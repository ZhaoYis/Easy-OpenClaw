using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// <see cref="GatewayConstants.Methods.Wake"/> RPC 的请求参数。
/// 用于调度一条「唤醒」用的文本注入：可立即下发（<see cref="WakeScheduleMode.Now"/>），
/// 或挂起到下一次 Agent 心跳时再注入（<see cref="WakeScheduleMode.NextHeartbeat"/>）。
/// 与上游 OpenClaw 协议 <c>WakeParamsSchema</c> 对齐。
/// </summary>
public sealed record WakeParams
{
    /// <summary>
    /// 调度模式：<c>now</c> 为立即注入；<c>next-heartbeat</c> 为下一次心跳时注入。
    /// </summary>
    [JsonPropertyName("mode")]
    public required string Mode { get; init; }

    /// <summary>
    /// 注入到 Agent 上下文的非空文本内容（系统事件/唤醒文案）。
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>
/// <see cref="WakeParams.Mode"/> 的合法取值常量，对应网关 schema 中的字面量枚举。
/// </summary>
public static class WakeScheduleMode
{
    /// <summary>立即执行唤醒文本注入</summary>
    public const string Now = "now";

    /// <summary>在下次心跳 tick 时再注入唤醒文本</summary>
    public const string NextHeartbeat = "next-heartbeat";
}
