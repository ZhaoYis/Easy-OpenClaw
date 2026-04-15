namespace OpenClaw.Core.SignalR;

/// <summary>
/// 将 OpenClaw <see cref="OpenClaw.Core.Client.GatewayClient"/> 的建连与网关事件订阅绑定到 Hub 连接数：
/// 首个客户端建连后启动，最后一个客户端断开后释放。
/// </summary>
public interface IOpenClawSignalRGatewayHubBridge
{
    /// <summary>在 Hub 完成分组与运营快照注册之后调用。</summary>
    /// <param name="context">当前连接 id 与解析出的用户 id</param>
    /// <param name="cancellationToken">取消桥接启动（如连接已中止）</param>
    Task OnHubConnectedAsync(OpenClawSignalRGatewayHubBridgeContext context, CancellationToken cancellationToken = default);

    /// <summary>在 Hub 断开流程中调用（建议在移除运营快照之后）。</summary>
    /// <param name="context">与建连时同一连接上下文</param>
    /// <param name="cancellationToken">取消断开清理</param>
    Task OnHubDisconnectedAsync(OpenClawSignalRGatewayHubBridgeContext context, CancellationToken cancellationToken = default);
}