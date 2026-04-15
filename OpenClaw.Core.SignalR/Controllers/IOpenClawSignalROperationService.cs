using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 运营侧查询在线连接、组分布，并向指定用户、连接或组调用客户端 Hub 方法。
/// </summary>
/// <typeparam name="THub">与 <c>MapHub&lt;THub&gt;</c> 一致的 Hub 类型。</typeparam>
/// <remarks>
/// 连接列表来自 <see cref="IOpenClawSignalRConnectionPresenceStore"/>（由
/// <see cref="OpenClawSignalRGatewayBuilder.UseMemoryStore"/>、
/// <see cref="OpenClawSignalRGatewayBuilder.UseHybridStore"/> 或自定义注册提供）。
/// <see cref="SendToUserAsync"/> 使用的 <paramref name="userId"/> 须与 JWT/Claims 中
/// <see cref="OpenClawSignalROptions.UserIdClaimType"/> 及 <see cref="OpenClawSignalRUserIdProvider"/> 解析结果一致。
/// </remarks>
public interface IOpenClawSignalROperationService<THub>
    where THub : Hub
{
    /// <summary>当前快照副本（按连接）。</summary>
    /// <param name="cancellationToken">取消查询</param>
    Task<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetOnlineConnectionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>当前连接数。</summary>
    /// <param name="cancellationToken">取消查询</param>
    Task<int> GetOnlineConnectionCountAsync(CancellationToken cancellationToken = default);

    /// <summary>已认证连接的去重用户 id（匿名连接不计入）。</summary>
    /// <param name="cancellationToken">取消查询</param>
    Task<IReadOnlyList<string>> GetDistinctOnlineUserIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>指定用户 id 下的连接（声明 id 与 <see cref="OpenClawSignalROptions.UserIdClaimType"/> 一致）。</summary>
    /// <param name="userId">用户标识</param>
    /// <param name="cancellationToken">取消查询</param>
    Task<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetConnectionsForUserAsync(string userId,
        CancellationToken cancellationToken = default);

    /// <summary>当前 <paramref name="user"/> 解析出的用户 id 在运营存储中的连接（无用户 id 声明时返回空列表）。</summary>
    /// <param name="user">通常为 <c>HttpContext.User</c></param>
    /// <param name="cancellationToken">取消查询</param>
    Task<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetConnectionsForCurrentUserAsync(ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    /// <summary>统计各 SignalR 组当前覆盖的连接数（同一连接在多个组中会分别计数）。</summary>
    /// <param name="cancellationToken">取消查询</param>
    Task<IReadOnlyDictionary<string, int>> GetSignalRGroupConnectionCountsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>与 Hub 加入的单用户组名一致，便于对组广播。</summary>
    /// <param name="userId">原始用户 id（内部会规范化）</param>
    string FormatUserGroupName(string userId);

    /// <summary>与 Hub 加入的档位组名一致。</summary>
    /// <param name="tier">档位 Claim 值</param>
    string FormatTierGroupName(string tier);

    /// <summary>向该用户的所有连接调用客户端方法（依赖 <see cref="IUserIdProvider"/>）。</summary>
    /// <param name="userId">与 Claims 中用户 id 一致</param>
    /// <param name="hubMethod">客户端方法名</param>
    /// <param name="args">位置参数，null 等价空数组</param>
    /// <param name="cancellationToken">取消发送</param>
    Task SendToUserAsync(string userId, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据运营存储中当前用户的 <see cref="OpenClawSignalRConnectionSnapshot.ConnectionId"/> 列表发送（与
    /// <see cref="SendToUserAsync"/> 数据源一致）；无用户 id 或无在线连接时不发送。
    /// </summary>
    /// <param name="user">当前用户主体</param>
    /// <param name="hubMethod">客户端方法名</param>
    /// <param name="args">位置参数</param>
    /// <param name="cancellationToken">取消发送</param>
    Task SendToCurrentUserConnectionsAsync(ClaimsPrincipal user, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default);

    /// <summary>向指定连接调用客户端方法。</summary>
    /// <param name="connectionId">SignalR 连接 id</param>
    /// <param name="hubMethod">客户端方法名</param>
    /// <param name="args">位置参数</param>
    /// <param name="cancellationToken">取消发送</param>
    Task SendToConnectionAsync(string connectionId, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default);

    /// <summary>向指定组调用客户端方法。</summary>
    /// <param name="groupName">组名</param>
    /// <param name="hubMethod">客户端方法名</param>
    /// <param name="args">位置参数</param>
    /// <param name="cancellationToken">取消发送</param>
    Task SendToGroupAsync(string groupName, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default);
}
