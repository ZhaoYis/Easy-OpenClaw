using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 使用 <see cref="IMemoryCache"/> 保存快照，并用锁 + <see cref="HashSet{T}"/> 维护可枚举的连接 id（不使用 <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>）；
/// 通常由 <see cref="OpenClawSignalRGatewayBuilder.UseMemoryConnectionPresence"/> 注册。
/// </summary>
public sealed class OpenClawSignalRMemoryConnectionPresenceStore : IOpenClawSignalRConnectionPresenceStore
{
    private readonly IMemoryCache _cache;
    private readonly IOptions<OpenClawSignalROptions> _options;
    private readonly object _idLock = new();
    private readonly HashSet<string> _connectionIds = new(StringComparer.Ordinal);

    public OpenClawSignalRMemoryConnectionPresenceStore(
        IMemoryCache cache,
        IOptions<OpenClawSignalROptions> options)
    {
        _cache = cache;
        _options = options;
    }

    /// <inheritdoc />
    public ValueTask RegisterAsync(OpenClawSignalRConnectionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = PayloadCacheKey(snapshot.ConnectionId);
        _cache.Set(key, snapshot, new MemoryCacheEntryOptions
        {
            // 由 Hub 断开显式移除；此处仅防止异常未清理时的泄漏
            SlidingExpiration = TimeSpan.FromDays(1),
        });

        lock (_idLock)
        {
            _connectionIds.Add(snapshot.ConnectionId);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _cache.Remove(PayloadCacheKey(connectionId));
        lock (_idLock)
        {
            _connectionIds.Remove(connectionId);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string[] idsCopy;
        lock (_idLock)
        {
            idsCopy = _connectionIds.ToArray();
        }

        var list = new List<OpenClawSignalRConnectionSnapshot>(idsCopy.Length);
        foreach (var id in idsCopy)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_cache.TryGetValue(PayloadCacheKey(id), out var obj) && obj is OpenClawSignalRConnectionSnapshot snap)
            {
                list.Add(snap);
            }
            else
            {
                lock (_idLock)
                {
                    _connectionIds.Remove(id);
                }
            }
        }

        return ValueTask.FromResult<IReadOnlyList<OpenClawSignalRConnectionSnapshot>>(list);
    }

    private string PayloadCacheKey(string connectionId) =>
        _options.Value.ConnectionPresencePayloadKeyPrefix + connectionId;
}
