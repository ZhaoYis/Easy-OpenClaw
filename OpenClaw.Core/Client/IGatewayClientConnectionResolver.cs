using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 可选地覆盖网关 WebSocket 地址与 TLS 指纹；返回 null 或空白字符串时表示使用 <paramref name="configuredOptions"/> 中的默认配置。
/// 用于多租户/多用户场景：同一套 <see cref="GatewayOptions"/> 绑定全局默认值，每个 <see cref="GatewayClient"/> 通过不同实现指向不同网关或指纹。
/// </summary>
public interface IGatewayClientConnectionResolver
{
    /// <summary>
    /// 解析实际建连使用的 WebSocket URL。
    /// </summary>
    /// <param name="state">建连上下文（如 SignalR Hub 桥接上下文）；无上下文时可传 null。</param>
    /// <param name="gatewayOptions">已绑定的全局/默认选项</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>非空白则使用该地址；否则由 <see cref="GatewayClient"/> 回退到 <see cref="GatewayOptions.Url"/>。</returns>
    ValueTask<string> ResolveWebSocketUrlAsync(object? state, GatewayOptions gatewayOptions, CancellationToken ct = default);

    /// <summary>
    /// 解析 TLS 证书指纹（证书固定）。
    /// </summary>
    /// <param name="state">建连上下文；无上下文时可传 null。</param>
    /// <param name="gatewayOptions">已绑定的全局/默认选项</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>非空白则使用该指纹；否则回退到 <see cref="GatewayOptions.TlsFingerprint"/>。</returns>
    ValueTask<string?> ResolveTlsFingerprintAsync(object? state, GatewayOptions gatewayOptions, CancellationToken ct = default);
}