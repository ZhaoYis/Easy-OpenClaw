using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 不覆盖连接参数：始终回退到 <see cref="GatewayOptions"/> 中的 Url 与 TlsFingerprint。
/// </summary>
public sealed class DefaultGatewayClientConnectionResolver : IGatewayClientConnectionResolver
{
    /// <summary>
    /// 解析实际建连使用的 WebSocket URL。
    /// </summary>
    /// <param name="state">建连上下文；本实现忽略。</param>
    /// <param name="gatewayOptions">已绑定的全局/默认选项；本实现忽略。</param>
    /// <param name="ct">取消令牌；本实现不发起异步 I/O。</param>
    /// <returns>始终返回空白字符串，由 <see cref="GatewayClient"/> 回退到 <see cref="GatewayOptions.Url"/>。</returns>
    public ValueTask<string> ResolveWebSocketUrlAsync(object? state, GatewayOptions gatewayOptions, CancellationToken ct) =>
        ValueTask.FromResult(string.Empty);

    /// <summary>
    /// 解析 TLS 证书指纹（证书固定）。
    /// </summary>
    /// <param name="state">建连上下文；本实现忽略。</param>
    /// <param name="gatewayOptions">已绑定的全局/默认选项；本实现忽略。</param>
    /// <param name="ct">取消令牌；本实现不发起异步 I/O。</param>
    /// <returns>始终返回 null，由 <see cref="GatewayClient"/> 回退到 <see cref="GatewayOptions.TlsFingerprint"/>。</returns>
    public ValueTask<string?> ResolveTlsFingerprintAsync(object? state, GatewayOptions gatewayOptions, CancellationToken ct) =>
        ValueTask.FromResult<string?>(null);
}