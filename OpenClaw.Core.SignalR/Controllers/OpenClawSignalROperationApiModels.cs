using System.Text.Json;

namespace OpenClaw.Core.SignalR;

/// <summary>POST <c>send/me</c> 请求体（用户身份来自 <c>HttpContext.User</c>）。</summary>
/// <param name="HubMethod">客户端 Hub 方法名（camelCase，与 JS 一致）</param>
/// <param name="Args">按位置传给客户端方法的 JSON 参数，可为 null 或空</param>
public sealed record OpenClawSignalRSendToCurrentUserRequest(
    string HubMethod,
    JsonElement[]? Args = null);

/// <summary>POST <c>send/user</c> 请求体。</summary>
/// <param name="UserId">与 <see cref="OpenClawSignalROptions.UserIdClaimType"/> / <see cref="OpenClawSignalRUserIdProvider"/> 一致</param>
/// <param name="HubMethod">客户端 Hub 方法名</param>
/// <param name="Args">位置参数 JSON 数组</param>
public sealed record OpenClawSignalRSendToUserRequest(
    string UserId,
    string HubMethod,
    JsonElement[]? Args = null);

/// <summary>POST <c>send/connection</c> 请求体。</summary>
/// <param name="ConnectionId">SignalR 连接 id</param>
/// <param name="HubMethod">客户端 Hub 方法名</param>
/// <param name="Args">位置参数 JSON 数组</param>
public sealed record OpenClawSignalRSendToConnectionRequest(
    string ConnectionId,
    string HubMethod,
    JsonElement[]? Args = null);

/// <summary>POST <c>send/group</c> 请求体。</summary>
/// <param name="GroupName">已存在的 SignalR 组名</param>
/// <param name="HubMethod">客户端 Hub 方法名</param>
/// <param name="Args">位置参数 JSON 数组</param>
public sealed record OpenClawSignalRSendToGroupRequest(
    string GroupName,
    string HubMethod,
    JsonElement[]? Args = null);

/// <summary>GET <c>groups/formatted/...</c> 的 JSON 响应。</summary>
/// <param name="GroupName">与 Hub 建连加入的组名规则一致</param>
public sealed record OpenClawSignalRFormattedGroupNameResponse(string GroupName);
