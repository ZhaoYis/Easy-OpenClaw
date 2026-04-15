using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 使用 <see cref="IMemoryCache"/> 保存快照，并用锁 + <see cref="HashSet{T}"/> 维护可枚举的索引 token（<c>userKeySegment|connectionId</c>）；
/// 通常由 <see cref="OpenClawSignalRGatewayBuilder.UseMemoryStore"/> 注册。
/// </summary>
public sealed class OpenClawSignalRMemoryConnectionPresenceStore : IOpenClawSignalRConnectionPresenceStore
{
    private readonly IMemoryCache _cache;
    private readonly IOptions<OpenClawSignalROptions> _options;

    private readonly object _idLock = new();
    private readonly HashSet<string> _indexTokens = new(StringComparer.Ordinal);

    /// <summary>注入内存缓存与选项（键前缀来自 <see cref="OpenClawSignalROptions.ConnectionPresencePayloadKeyPrefix"/>）。</summary>
    public OpenClawSignalRMemoryConnectionPresenceStore(IMemoryCache cache, IOptions<OpenClawSignalROptions> options)
    {
        _cache = cache;
        _options = options;
    }

    /// <summary>在 Hub 建连成功后写入或更新一条连接快照（与 <see cref="OpenClawGatewayHubBase.OnConnectedAsync"/> 同步）。</summary>
    /// <param name="snapshot">含连接 id、用户、组与身份快照</param>
    /// <param name="cancellationToken">取消注册</param>
    /// <remarks>使用 <see cref="IMemoryCache"/> 与进程内 <see cref="HashSet{T}"/> 索引，非分布式。</remarks>
    public ValueTask RegisterAsync(OpenClawSignalRConnectionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var o = _options.Value;
        var payloadKey = OpenClawSignalRConnectionPresenceKeys.PayloadKeyForSnapshot(snapshot, o);
        var indexToken = OpenClawSignalRConnectionPresenceKeys.IndexTokenForSnapshot(snapshot, o);

        _cache.Set(payloadKey, snapshot, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromDays(1),
        });

        lock (_idLock)
        {
            _indexTokens.Add(indexToken);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>在 Hub 断开时移除对应载荷与索引项。</summary>
    /// <param name="connectionId">SignalR 连接 id</param>
    /// <param name="presenceUserId">与注册时 <see cref="OpenClawSignalRConnectionSnapshot.UserId"/> 一致（匿名连接为 <c>null</c>）</param>
    /// <param name="cancellationToken">取消移除</param>
    public ValueTask RemoveAsync(string connectionId, string? presenceUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var o = _options.Value;
        var payloadKey = OpenClawSignalRConnectionPresenceKeys.PayloadKeyForRemoval(connectionId, presenceUserId, o);
        var indexToken = OpenClawSignalRConnectionPresenceKeys.IndexTokenForRemoval(connectionId, presenceUserId, o);

        _cache.Remove(payloadKey);
        lock (_idLock)
        {
            _indexTokens.Remove(indexToken);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>返回当前缓存中所有快照的副本（运营查询、受众解析枚举）。</summary>
    /// <param name="cancellationToken">取消枚举</param>
    public ValueTask<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var o = _options.Value;
        string[] tokensCopy;
        lock (_idLock)
        {
            tokensCopy = _indexTokens.ToArray();
        }

        var list = new List<OpenClawSignalRConnectionSnapshot>(tokensCopy.Length);
        foreach (var token in tokensCopy)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!OpenClawSignalRConnectionPresenceKeys.TryParseIndexToken(token, out var userSeg, out var connId))
            {
                lock (_idLock)
                {
                    _indexTokens.Remove(token);
                }

                continue;
            }

            var payloadKey = OpenClawSignalRConnectionPresenceKeys.FormatPayloadKey(
                o.ConnectionPresencePayloadKeyPrefix,
                userSeg,
                connId);

            if (_cache.TryGetValue(payloadKey, out var obj) && obj is OpenClawSignalRConnectionSnapshot snap)
            {
                list.Add(snap);
            }
            else
            {
                lock (_idLock)
                {
                    _indexTokens.Remove(token);
                }
            }
        }

        return ValueTask.FromResult<IReadOnlyList<OpenClawSignalRConnectionSnapshot>>(list);
    }
}