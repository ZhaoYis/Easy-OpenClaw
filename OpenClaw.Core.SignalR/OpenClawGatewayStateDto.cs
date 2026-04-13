using OpenClaw.Core.Client;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 供 SignalR 客户端查询的网关连接摘要（不包含完整 hello-ok 载荷）。
/// </summary>
public sealed record OpenClawGatewayStateDto(
    bool IsConnected,
    ConnectionState State,
    IReadOnlyList<string> AvailableMethods,
    IReadOnlyList<string> AvailableEvents);
