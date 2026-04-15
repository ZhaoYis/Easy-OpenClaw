using OpenClaw.Core.Client;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// SignalR 场景下的 <see cref="IGatewayClientConnectionResolver"/>：当前始终回退到 <see cref="GatewayOptions.Url"/> / 指纹；
/// 预留按 <see cref="OpenClawSignalRGatewayHubBridgeContext"/> 解析每用户网关地址。
/// </summary>
public sealed class OpenClawSignalRGatewayClientConnectionResolver : IGatewayClientConnectionResolver
{
    /// <summary>
    /// 解析实际建连使用的 WebSocket URL。
    /// </summary>
    /// <param name="state">桥接上下文等；当前实现不据此覆盖 URL。</param>
    /// <param name="gatewayOptions">全局选项；当前实现不使用。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>空白字符串，由 <see cref="GatewayClient"/> 使用 <see cref="GatewayOptions.Url"/>。</returns>
    public ValueTask<string> ResolveWebSocketUrlAsync(object? state, GatewayOptions gatewayOptions, CancellationToken ct)
    {
        _ = state is OpenClawSignalRGatewayHubBridgeContext;
        // 预留：可按 context.UserId 等为不同 Hub 连接解析独立网关地址；当前回退到配置 Url。
        return ValueTask.FromResult(string.Empty);
    }

    /// <summary>
    /// 解析 TLS 证书指纹（证书固定）。
    /// </summary>
    /// <param name="state">桥接上下文等；当前实现不据此覆盖指纹。</param>
    /// <param name="gatewayOptions">全局选项；当前实现不使用。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>null，由 <see cref="GatewayClient"/> 使用 <see cref="GatewayOptions.TlsFingerprint"/>。</returns>
    public ValueTask<string?> ResolveTlsFingerprintAsync(object? state, GatewayOptions gatewayOptions, CancellationToken ct) =>
        ValueTask.FromResult<string?>(null);
}