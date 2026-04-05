using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// <see cref="GatewayConstants.Methods.ChatInject"/> RPC 方法的请求参数，
/// 用于向聊天会话直接注入一条消息，不触发 Agent 回复生成。
/// 典型用途：插入系统提示、旁白、上下文修正或外部工具返回的内容。
/// </summary>
public sealed record ChatInjectParams
{
    /// <summary>目标会话的唯一键。为 null 时使用默认会话。</summary>
    [JsonPropertyName("sessionKey")]
    public string? SessionKey { get; init; }

    /// <summary>
    /// 注入的消息角色（如 "user"、"assistant"、"system"）。
    /// 不同角色决定消息在对话上下文中的位置和 Agent 的解读方式。
    /// </summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    /// <summary>注入的消息文本内容</summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// 可选的结构化附加载荷（如工具调用结果 JSON），
    /// 与 content 一起注入到消息记录中。
    /// </summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    /// <summary>幂等键，防止重复注入；默认自动生成 UUID</summary>
    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; init; } = Guid.NewGuid().ToString();
}