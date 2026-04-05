using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// <see cref="GatewayConstants.Methods.Send"/> RPC 方法的请求参数。
/// 用于在聊天运行器（chat runner）之外，直接向指定渠道/账号/线程投递出站消息。
/// </summary>
public sealed record SendParams
{
    /// <summary>
    /// 目标渠道标识（如 "telegram"、"discord"、"wechat"、"slack" 等）。
    /// 必须与网关中已配置的渠道名称匹配。
    /// </summary>
    [JsonPropertyName("channel")]
    public required string Channel { get; init; }

    /// <summary>
    /// 目标账号/用户/群组标识。格式因渠道而异：
    /// Telegram 为 chatId、Discord 为 channelId、微信为 wxid 等。
    /// </summary>
    [JsonPropertyName("account")]
    public required string Account { get; init; }

    /// <summary>
    /// 目标线程/话题 ID，用于支持线程化消息的渠道（如 Slack threads、Discord forum threads）。
    /// 为 null 时消息投递到主对话。
    /// </summary>
    [JsonPropertyName("threadId")]
    public string? ThreadId { get; init; }

    /// <summary>
    /// 消息文本内容。当 <see cref="Payload"/> 未提供时，此字段为必填。
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// 结构化消息载荷，用于发送富文本、卡片、按钮等渠道原生格式的消息。
    /// 当同时提供 <see cref="Text"/> 时，Payload 优先级更高。
    /// </summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    /// <summary>
    /// 幂等键，防止因网络重试导致重复投递。默认自动生成 UUID。
    /// </summary>
    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; init; } = Guid.NewGuid().ToString();
}
