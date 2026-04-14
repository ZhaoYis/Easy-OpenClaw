namespace OpenClaw.Core.SignalR;

/// <summary>
/// 维护 SignalR 连接运营快照（与 Hub 生命周期同步）。
/// </summary>
public interface IOpenClawSignalRConnectionPresenceStore
{
    ValueTask RegisterAsync(OpenClawSignalRConnectionSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <param name="presenceUserId">与注册时 <see cref="OpenClawSignalRConnectionSnapshot.UserId"/> 一致（匿名连接为 <c>null</c>）。</param>
    ValueTask RemoveAsync(string connectionId, string? presenceUserId, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default);
}