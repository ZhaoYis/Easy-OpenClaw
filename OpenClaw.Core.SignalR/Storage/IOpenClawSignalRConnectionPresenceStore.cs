namespace OpenClaw.Core.SignalR;

/// <summary>
/// 维护 SignalR 连接运营快照（与 Hub 生命周期同步）。
/// </summary>
public interface IOpenClawSignalRConnectionPresenceStore
{
    ValueTask RegisterAsync(OpenClawSignalRConnectionSnapshot snapshot, CancellationToken cancellationToken = default);

    ValueTask RemoveAsync(string connectionId, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default);
}
