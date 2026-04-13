using System.Text.Json;

namespace OpenClaw.Core.SignalR;

/// <summary>POST <c>send/user</c> 请求体。</summary>
public sealed record OpenClawSignalRSendToUserRequest(
    string UserId,
    string HubMethod,
    JsonElement[]? Args = null);

/// <summary>POST <c>send/connection</c> 请求体。</summary>
public sealed record OpenClawSignalRSendToConnectionRequest(
    string ConnectionId,
    string HubMethod,
    JsonElement[]? Args = null);

/// <summary>POST <c>send/group</c> 请求体。</summary>
public sealed record OpenClawSignalRSendToGroupRequest(
    string GroupName,
    string HubMethod,
    JsonElement[]? Args = null);

/// <summary>GET <c>groups/formatted/...</c> 的 JSON 响应。</summary>
public sealed record OpenClawSignalRFormattedGroupNameResponse(string GroupName);
