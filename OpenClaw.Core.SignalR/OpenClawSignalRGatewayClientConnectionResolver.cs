using OpenClaw.Core.Client;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.SignalR;

public sealed class OpenClawSignalRGatewayClientConnectionResolver : IGatewayClientConnectionResolver
{
    public ValueTask<string> ResolveWebSocketUrlAsync(object? state, GatewayOptions gatewayOptions, CancellationToken ct)
    {
        _ = state is OpenClawSignalRGatewayHubBridgeContext;
        // 预留：可按 context.UserId 等为不同 Hub 连接解析独立网关地址；当前回退到配置 Url。
        return ValueTask.FromResult(string.Empty);
    }

    public ValueTask<string?> ResolveTlsFingerprintAsync(object? state, GatewayOptions gatewayOptions, CancellationToken ct) =>
        ValueTask.FromResult<string?>(null);
}