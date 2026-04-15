using OpenClaw.Core.Client;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 供 SignalR 客户端查询的网关连接摘要（不包含完整 hello-ok 载荷）。
/// </summary>
/// <param name="IsConnected">底层 WebSocket 是否已连接</param>
/// <param name="State"><see cref="GatewayClient"/> 的连接状态枚举</param>
/// <param name="AvailableMethods">hello-ok 中声明的 RPC 方法名</param>
/// <param name="AvailableEvents">hello-ok 中声明的可推送事件名</param>
public sealed record OpenClawGatewayStateDto(
    bool IsConnected,
    ConnectionState State,
    IReadOnlyList<string> AvailableMethods,
    IReadOnlyList<string> AvailableEvents);
