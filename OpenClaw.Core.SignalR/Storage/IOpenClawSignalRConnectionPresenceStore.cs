namespace OpenClaw.Core.SignalR;

/// <summary>
/// 维护 SignalR 连接运营快照（与 Hub 生命周期同步）。
/// </summary>
public interface IOpenClawSignalRConnectionPresenceStore
{
    /// <summary>在 Hub 建连成功后写入或更新一条连接快照（与 <see cref="OpenClawGatewayHubBase.OnConnectedAsync"/> 同步）。</summary>
    /// <param name="snapshot">含连接 id、用户、组与身份快照</param>
    /// <param name="cancellationToken">取消注册</param>
    ValueTask RegisterAsync(OpenClawSignalRConnectionSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>在 Hub 断开时移除对应载荷与索引项。</summary>
    /// <param name="connectionId">SignalR 连接 id</param>
    /// <param name="presenceUserId">与注册时 <see cref="OpenClawSignalRConnectionSnapshot.UserId"/> 一致（匿名连接为 <c>null</c>）。</param>
    /// <param name="cancellationToken">取消移除</param>
    ValueTask RemoveAsync(string connectionId, string? presenceUserId, CancellationToken cancellationToken = default);

    /// <summary>返回当前缓存中所有快照的副本（运营查询、受众解析枚举）。</summary>
    /// <param name="cancellationToken">取消枚举</param>
    ValueTask<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default);
}