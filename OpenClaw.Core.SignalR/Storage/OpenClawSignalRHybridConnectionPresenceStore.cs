using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 使用 <see cref="HybridCache"/> 存储连接快照，<see cref="OpenClawSignalRDistributedConnectionIdIndex"/> 维护跨实例索引 token 列表；
/// 通常由 <see cref="OpenClawSignalRGatewayBuilder.UseHybridStore"/> 注册。
/// </summary>
public sealed class OpenClawSignalRHybridConnectionPresenceStore : IOpenClawSignalRConnectionPresenceStore
{
    private static readonly HybridCacheEntryOptions PresenceEntryOptions = new()
    {
        Expiration = TimeSpan.FromDays(7),
        LocalCacheExpiration = TimeSpan.FromMinutes(30),
    };

    private readonly HybridCache _hybrid;
    private readonly OpenClawSignalRDistributedConnectionIdIndex _index;
    private readonly IOptions<OpenClawSignalROptions> _options;

    /// <summary>注入 Hybrid 缓存、分布式索引与选项。</summary>
    public OpenClawSignalRHybridConnectionPresenceStore(
        HybridCache hybrid,
        OpenClawSignalRDistributedConnectionIdIndex index,
        IOptions<OpenClawSignalROptions> options)
    {
        _hybrid = hybrid;
        _index = index;
        _options = options;
    }

    /// <summary>在 Hub 建连成功后写入或更新一条连接快照（与 <see cref="OpenClawGatewayHubBase.OnConnectedAsync"/> 同步）。</summary>
    /// <param name="snapshot">含连接 id、用户、组与身份快照</param>
    /// <param name="cancellationToken">取消注册</param>
    /// <remarks>载荷写入 <see cref="HybridCache"/>，索引 token 写入 <see cref="OpenClawSignalRDistributedConnectionIdIndex"/>。</remarks>
    public async ValueTask RegisterAsync(OpenClawSignalRConnectionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var o = _options.Value;
        var payloadKey = OpenClawSignalRConnectionPresenceKeys.PayloadKeyForSnapshot(snapshot, o);
        var indexToken = OpenClawSignalRConnectionPresenceKeys.IndexTokenForSnapshot(snapshot, o);

        await _hybrid.SetAsync(
                payloadKey,
                snapshot,
                PresenceEntryOptions,
                tags: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await _index.AddAsync(indexToken, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>在 Hub 断开时移除对应载荷与索引项。</summary>
    /// <param name="connectionId">SignalR 连接 id</param>
    /// <param name="presenceUserId">与注册时 <see cref="OpenClawSignalRConnectionSnapshot.UserId"/> 一致（匿名连接为 <c>null</c>）</param>
    /// <param name="cancellationToken">取消移除</param>
    public async ValueTask RemoveAsync(string connectionId, string? presenceUserId, CancellationToken cancellationToken = default)
    {
        var o = _options.Value;
        var payloadKey = OpenClawSignalRConnectionPresenceKeys.PayloadKeyForRemoval(connectionId, presenceUserId, o);
        var indexToken = OpenClawSignalRConnectionPresenceKeys.IndexTokenForRemoval(connectionId, presenceUserId, o);

        await _hybrid.RemoveAsync(payloadKey, cancellationToken).ConfigureAwait(false);
        await _index.RemoveAsync(indexToken, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>返回当前缓存中所有快照的副本（运营查询、受众解析枚举）。</summary>
    /// <param name="cancellationToken">取消枚举</param>
    public async ValueTask<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        var o = _options.Value;
        var tokens = await _index.GetAllIndexTokensAsync(cancellationToken).ConfigureAwait(false);
        if (tokens.Count == 0)
            return [];

        var list = new List<OpenClawSignalRConnectionSnapshot>(tokens.Count);
        foreach (var token in tokens)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!OpenClawSignalRConnectionPresenceKeys.TryParseIndexToken(token, out var userSeg, out var connId))
            {
                await _index.RemoveAsync(token, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var payloadKey = OpenClawSignalRConnectionPresenceKeys.FormatPayloadKey(
                o.ConnectionPresencePayloadKeyPrefix,
                userSeg,
                connId);

            try
            {
                var snap = await _hybrid.GetOrCreateAsync(
                    payloadKey,
                    static _ => ValueTask.FromException<OpenClawSignalRConnectionSnapshot>(new KeyNotFoundException()),
                    PresenceEntryOptions,
                    tags: null,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                list.Add(snap);
            }
            catch (KeyNotFoundException)
            {
                await _index.RemoveAsync(token, cancellationToken).ConfigureAwait(false);
            }
        }

        return list;
    }
}
