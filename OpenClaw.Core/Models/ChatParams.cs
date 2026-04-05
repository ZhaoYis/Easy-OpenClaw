using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// chat.send RPC 方法的请求参数。
/// </summary>
public sealed record ChatSendParams
{
    /// <summary>目标会话的唯一标识键（如 "agent:main:main"）</summary>
    [JsonPropertyName("sessionKey")]
    public required string SessionKey { get; init; }

    /// <summary>用户发送的消息文本</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>是否通过配置的渠道（如 Telegram、Discord）投递消息，默认仅在当前会话内处理</summary>
    [JsonPropertyName("deliver")]
    public bool Deliver { get; init; } = false;

    /// <summary>幂等键，防止重复提交；默认自动生成 UUID</summary>
    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; init; } = Guid.NewGuid().ToString();
}
