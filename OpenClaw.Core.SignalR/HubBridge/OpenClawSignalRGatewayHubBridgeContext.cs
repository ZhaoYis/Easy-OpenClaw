namespace OpenClaw.Core.SignalR;

/// <summary>
/// Hub 与 OpenClaw 网关桥接时的一次建连或断开上下文；<see cref="UserId"/> 与运营快照一致（匿名或未解析出 Claim 时为 null）。
/// </summary>
public sealed record OpenClawSignalRGatewayHubBridgeContext(
    string ConnectionId,
    string? UserId);