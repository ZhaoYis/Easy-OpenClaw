using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// <see cref="GatewayConstants.Methods.ToolsEffective"/> RPC 的请求参数。
/// 网关根据 <see cref="SessionKey"/> 在服务端推导可信运行时上下文，返回该会话当前实际可用的工具集合
/// （含 core、plugin、channel 来源），调用方需具备 operator.read 权限。
/// </summary>
public sealed record ToolsEffectiveParams
{
    /// <summary>
    /// 会话键（必填），例如 <see cref="GatewayConstants.DefaultSessionKey"/> 或 <c>agent:main:main</c>。
    /// </summary>
    [JsonPropertyName("sessionKey")]
    public required string SessionKey { get; init; }

    /// <summary>
    /// 可选 Agent ID；省略时使用与会话关联的默认 Agent。
    /// </summary>
    [JsonPropertyName("agentId")]
    public string? AgentId { get; init; }
}
