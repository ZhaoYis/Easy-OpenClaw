using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// <see cref="GatewayConstants.Methods.SessionsCreate"/> RPC 方法的请求参数，
/// 用于创建一个新的会话条目。
/// </summary>
public sealed record SessionsCreateParams
{
    /// <summary>
    /// 新会话的 Agent 标识符。为 null 时使用网关默认 Agent。
    /// </summary>
    [JsonPropertyName("agentId")]
    public string? AgentId { get; init; }

    /// <summary>
    /// 新会话的键名。为 null 时由网关自动生成。
    /// </summary>
    [JsonPropertyName("sessionKey")]
    public string? SessionKey { get; init; }

    /// <summary>
    /// 会话的人类可读标题。
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// 会话的元数据标签（键值对），可用于分类和筛选。
    /// </summary>
    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; init; }

    /// <summary>
    /// 会话级的配置覆盖（如模型、temperature 等），深度合并到 Agent 默认配置之上。
    /// </summary>
    [JsonPropertyName("overrides")]
    public JsonElement? Overrides { get; init; }
}

/// <summary>
/// <see cref="GatewayConstants.Methods.SessionsSend"/> RPC 方法的请求参数，
/// 用于向已有会话发送一条消息。
/// </summary>
public sealed record SessionsSendParams
{
    /// <summary>目标会话的唯一键</summary>
    [JsonPropertyName("sessionKey")]
    public required string SessionKey { get; init; }

    /// <summary>用户发送的消息文本</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// 是否通过配置的渠道（如 Telegram、Discord）投递消息，默认仅在当前会话内处理。
    /// </summary>
    [JsonPropertyName("deliver")]
    public bool Deliver { get; init; } = false;

    /// <summary>幂等键，防止重复提交；默认自动生成 UUID</summary>
    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; init; } = Guid.NewGuid().ToString();
}

/// <summary>
/// <see cref="GatewayConstants.Methods.SessionsSteer"/> RPC 方法的请求参数，
/// 用于中断当前活跃会话并以新指令转向（interrupt-and-steer）。
/// </summary>
public sealed record SessionsSteerParams
{
    /// <summary>目标会话的唯一键</summary>
    [JsonPropertyName("sessionKey")]
    public required string SessionKey { get; init; }

    /// <summary>新的转向消息/指令文本，Agent 将中断当前处理并以此消息重新开始</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>幂等键，防止重复提交；默认自动生成 UUID</summary>
    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; init; } = Guid.NewGuid().ToString();
}

/// <summary>
/// <see cref="GatewayConstants.Methods.SessionsResolve"/> RPC 方法的请求参数，
/// 与上游 OpenClaw 网关 <c>SessionsResolveParamsSchema</c> 对齐（根对象 <c>additionalProperties: false</c>）。
/// </summary>
/// <remarks>
/// 业务约束（由网关在运行时校验）：<c>key</c>、<c>sessionId</c>、<c>label</c> 三者须**恰好**指定其一，
/// 不可全缺也不可多选。字段名须为协议中的 <c>key</c> 等；<c>target</c>、<c>sessionKey</c> 均非合法属性。
/// </remarks>
public sealed record SessionsResolveParams
{
    /// <summary>按会话键（可部分/模糊）解析时使用的非空字符串。</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>按会话 ID 解析时使用的非空字符串。</summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    /// <summary>按会话标签（label）解析时使用的字符串。</summary>
    [JsonPropertyName("label")]
    public string? Label { get; init; }

    /// <summary>可选；限制在指定 Agent 下解析。</summary>
    [JsonPropertyName("agentId")]
    public string? AgentId { get; init; }

    /// <summary>可选；仅考虑由某会话派生（spawn）的会话时的过滤键。</summary>
    [JsonPropertyName("spawnedBy")]
    public string? SpawnedBy { get; init; }

    /// <summary>可选；是否在结果中纳入全局会话。</summary>
    [JsonPropertyName("includeGlobal")]
    public bool? IncludeGlobal { get; init; }

    /// <summary>可选；是否在结果中纳入未知会话。</summary>
    [JsonPropertyName("includeUnknown")]
    public bool? IncludeUnknown { get; init; }
}

/// <summary>
/// <see cref="GatewayConstants.Methods.SessionsPreview"/> RPC 方法的请求参数，
/// 与上游 OpenClaw <c>SessionsPreviewParamsSchema</c> 对齐（根对象 <c>additionalProperties: false</c>）。
/// </summary>
/// <remarks>
/// 必填字段为 JSON 属性 <c>keys</c>（非 <c>sessionKey</c> / <c>sessionKeys</c>）；至少包含一个非空会话键字符串。
/// </remarks>
public sealed record SessionsPreviewParams
{
    /// <summary>要预览的会话键列表，序列化为协议字段 <c>keys</c>。</summary>
    [JsonPropertyName("keys")]
    public required string[] Keys { get; init; }

    /// <summary>每个会话返回的最大消息条数，为 null 时由网关决定默认值。</summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>可选；每条预览文本的最大字符数下限由网关校验（通常 ≥ 20）。</summary>
    [JsonPropertyName("maxChars")]
    public int? MaxChars { get; init; }
}

/// <summary>
/// <see cref="GatewayConstants.Methods.SessionsMessagesSubscribe"/> 与
/// <see cref="GatewayConstants.Methods.SessionsMessagesUnsubscribe"/> 共用的请求参数，
/// 与上游 OpenClaw <c>SessionsMessagesSubscribeParamsSchema</c> / <c>Unsubscribe</c> 对齐。
/// </summary>
/// <remarks>
/// 协议字段名为 <c>key</c>（非 <c>sessionKey</c>）；根对象 <c>additionalProperties: false</c>。
/// 官方说明：二者用于 toggle transcript/message event subscriptions for one session。
/// </remarks>
public sealed record SessionsMessagesKeyParams
{
    /// <summary>目标会话键（规范或待规范化的 key），序列化为 JSON 属性 <c>key</c>。</summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }
}

/// <summary>
/// <see cref="GatewayConstants.Methods.SessionsUsage"/> RPC 的请求参数，
/// 与上游 OpenClaw <c>SessionsUsageParamsSchema</c> 对齐（根对象 <c>additionalProperties: false</c>）。
/// </summary>
/// <remarks>
/// 指定会话时使用 JSON 属性 <c>key</c>（非 <c>sessionKey</c>）；省略 <c>key</c> 时网关可返回多会话汇总（受 <c>limit</c> 等约束）。
/// </remarks>
public sealed record SessionsUsageParams
{
    /// <summary>要分析的具体会话键；为 null 时不发送该字段，由网关返回多会话视图。</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>范围起始日期，格式 <c>YYYY-MM-DD</c>。</summary>
    [JsonPropertyName("startDate")]
    public string? StartDate { get; init; }

    /// <summary>范围结束日期，格式 <c>YYYY-MM-DD</c>。</summary>
    [JsonPropertyName("endDate")]
    public string? EndDate { get; init; }

    /// <summary>解释 <c>startDate</c>/<c>endDate</c> 的方式：<c>utc</c>、<c>gateway</c> 或 <c>specific</c>。</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    /// <summary><c>mode=specific</c> 时使用的 UTC 偏移字符串（如 <c>UTC+8</c>、<c>UTC-4:30</c>）。</summary>
    [JsonPropertyName("utcOffset")]
    public string? UtcOffset { get; init; }

    /// <summary>最多返回的会话条数；为 null 时网关默认 50。</summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>为 true 时在结果中包含上下文权重分解（如 systemPromptReport）。</summary>
    [JsonPropertyName("includeContextWeight")]
    public bool? IncludeContextWeight { get; init; }
}

/// <summary>
/// <see cref="GatewayConstants.Methods.SessionsUsageTimeseries"/> 的请求参数；
/// 上游实现仅要求非空 <c>key</c>（无 <c>from</c>/<c>to</c>/<c>granularity</c> 等字段）。
/// </summary>
public sealed record SessionsUsageTimeseriesParams
{
    /// <summary>目标会话键。</summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }
}

/// <summary>
/// <see cref="GatewayConstants.Methods.SessionsUsageLogs"/> 的请求参数；上游使用 <c>key</c> 与可选 <c>limit</c>（默认 200，最大 1000）。
/// </summary>
public sealed record SessionsUsageLogsParams
{
    /// <summary>目标会话键。</summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>返回条数上限；为 null 时使用网关默认。</summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }
}
