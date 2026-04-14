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

    public OpenClawSignalRMemoryConnectionPresenceStore(IMemoryCache cache, IOptions<OpenClawSignalROptions> options)
    {
        _cache = cache;
        _options = options;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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