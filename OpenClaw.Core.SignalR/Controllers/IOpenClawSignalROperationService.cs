using Microsoft.AspNetCore.SignalR;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 运营侧查询在线连接、组分布，并向指定用户、连接或组调用客户端 Hub 方法。
/// </summary>
/// <typeparam name="THub">与 <c>MapHub&lt;THub&gt;</c> 一致的 Hub 类型。</typeparam>
/// <remarks>
/// 连接列表来自 <see cref="IOpenClawSignalRConnectionPresenceStore"/>（由
/// <see cref="OpenClawSignalRGatewayBuilder.UseMemoryConnectionPresence"/>、
/// <see cref="OpenClawSignalRGatewayBuilder.UseHybridConnectionPresence"/> 或自定义注册提供）。
/// <see cref="SendToUserAsync"/> 使用的 <paramref name="userId"/> 须与 JWT/Claims 中
/// <see cref="OpenClawSignalROptions.UserIdClaimType"/> 及 <see cref="OpenClawSignalRUserIdProvider"/> 解析结果一致。
/// </remarks>
public interface IOpenClawSignalROperationService<THub>
    where THub : Hub
{
    /// <summary>当前快照副本（按连接）。</summary>
    Task<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetOnlineConnectionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>当前连接数。</summary>
    Task<int> GetOnlineConnectionCountAsync(CancellationToken cancellationToken = default);

    /// <summary>已认证连接的去重用户 id（匿名连接不计入）。</summary>
    Task<IReadOnlyList<string>> GetDistinctOnlineUserIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>指定用户 id 下的连接（声明 id 与 <see cref="OpenClawSignalROptions.UserIdClaimType"/> 一致）。</summary>
    Task<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetConnectionsForUserAsync(string userId,
        CancellationToken cancellationToken = default);

    /// <summary>统计各 SignalR 组当前覆盖的连接数（同一连接在多个组中会分别计数）。</summary>
    Task<IReadOnlyDictionary<string, int>> GetSignalRGroupConnectionCountsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>与 Hub 加入的单用户组名一致，便于对组广播。</summary>
    string FormatUserGroupName(string userId);

    /// <summary>与 Hub 加入的档位组名一致。</summary>
    string FormatTierGroupName(string tier);

    /// <summary>向该用户的所有连接调用客户端方法（依赖 <see cref="IUserIdProvider"/>）。</summary>
    Task SendToUserAsync(string userId, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default);

    /// <summary>向指定连接调用客户端方法。</summary>
    Task SendToConnectionAsync(string connectionId, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default);

    /// <summary>向指定组调用客户端方法。</summary>
    Task SendToGroupAsync(string groupName, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default);
}
