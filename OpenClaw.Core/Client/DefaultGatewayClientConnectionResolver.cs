using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 不覆盖连接参数：始终回退到 <see cref="GatewayOptions"/> 中的 Url 与 TlsFingerprint。
/// </summary>
public sealed class DefaultGatewayClientConnectionResolver : IGatewayClientConnectionResolver
{
    /// <inheritdoc />
    /// <remarks>返回空白 URL，由 <see cref="GatewayClient"/> 回退到 <see cref="GatewayOptions.Url"/>。</remarks>
    public ValueTask<string> ResolveWebSocketUrlAsync(object? state, GatewayOptions gatewayOptions, CancellationToken ct) =>
        ValueTask.FromResult(string.Empty);

    /// <inheritdoc />
    public ValueTask<string?> ResolveTlsFingerprintAsync(object? state, GatewayOptions gatewayOptions, CancellationToken ct) =>
        ValueTask.FromResult<string?>(null);
}