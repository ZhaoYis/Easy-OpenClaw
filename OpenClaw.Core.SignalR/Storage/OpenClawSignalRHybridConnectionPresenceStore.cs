using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 使用 <see cref="HybridCache"/> 存储连接快照，<see cref="OpenClawSignalRDistributedConnectionIdIndex"/> 维护跨实例连接 id 列表；
/// 通常由 <see cref="OpenClawSignalRGatewayBuilder.UseHybridConnectionPresence"/> 注册。
/// </summary>
public sealed class OpenClawSignalRHybridConnectionPresenceStore : IOpenClawSignalRConnectionPresenceStore
{
    private static readonly HybridCacheEntryOptions PresenceEntryOptions = new()
    {
        // 正常由 Hub 断开时 Remove；此处兜底防止进程异常后远端条目永久残留
        Expiration = TimeSpan.FromDays(7),
        LocalCacheExpiration = TimeSpan.FromMinutes(30),
    };

    private readonly HybridCache _hybrid;
    private readonly OpenClawSignalRDistributedConnectionIdIndex _index;
    private readonly IOptions<OpenClawSignalROptions> _options;

    public OpenClawSignalRHybridConnectionPresenceStore(
        HybridCache hybrid,
        OpenClawSignalRDistributedConnectionIdIndex index,
        IOptions<OpenClawSignalROptions> options)
    {
        _hybrid = hybrid;
        _index = index;
        _options = options;
    }

    /// <inheritdoc />
    public async ValueTask RegisterAsync(OpenClawSignalRConnectionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _hybrid.SetAsync(
                PayloadKey(snapshot.ConnectionId),
                snapshot,
                PresenceEntryOptions,
                tags: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await _index.AddAsync(snapshot.ConnectionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        await _hybrid.RemoveAsync(PayloadKey(connectionId), cancellationToken).ConfigureAwait(false);
        await _index.RemoveAsync(connectionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        var ids = await _index.GetAllIdsAsync(cancellationToken).ConfigureAwait(false);
        if (ids.Count == 0)
            return [];

        var list = new List<OpenClawSignalRConnectionSnapshot>(ids.Count);
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var snap = await _hybrid.GetOrCreateAsync(
                    PayloadKey(id),
                    static _ => ValueTask.FromException<OpenClawSignalRConnectionSnapshot>(new KeyNotFoundException()),
                    PresenceEntryOptions,
                    tags: null,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                list.Add(snap);
            }
            catch (KeyNotFoundException)
            {
                await _index.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
            }
        }

        return list;
    }

    private string PayloadKey(string connectionId) =>
        _options.Value.ConnectionPresencePayloadKeyPrefix + connectionId;
}
