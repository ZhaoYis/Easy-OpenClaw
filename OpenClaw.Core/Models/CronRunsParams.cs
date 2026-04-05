using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// <see cref="GatewayConstants.Methods.CronRuns"/> RPC 的请求参数。
/// 用于分页查询定时任务执行日志，可按任务 id、状态、投递状态等过滤。
/// </summary>
public sealed record CronRunsParams
{
    /// <summary>
    /// 查询范围：<c>job</c> 表示仅查单任务（需配合 <see cref="Id"/> 或 <see cref="JobId"/>）；<c>all</c> 表示全局。
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    /// <summary>任务 ID（与 <see cref="JobId"/> 二选一，对应 schema 中的 <c>id</c>）</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>任务 ID 的别名字段，与 <c>id</c> 在协议中互为替代</summary>
    [JsonPropertyName("jobId")]
    public string? JobId { get; init; }

    /// <summary>分页大小，1–200</summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>分页偏移</summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; init; }

    /// <summary>按多条运行状态过滤（ok / error / skipped）</summary>
    [JsonPropertyName("statuses")]
    public string[]? Statuses { get; init; }

    /// <summary>单条状态过滤：<c>all</c>、<c>ok</c>、<c>error</c>、<c>skipped</c></summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>按投递状态多选过滤</summary>
    [JsonPropertyName("deliveryStatuses")]
    public string[]? DeliveryStatuses { get; init; }

    /// <summary>单条投递状态过滤</summary>
    [JsonPropertyName("deliveryStatus")]
    public string? DeliveryStatus { get; init; }

    /// <summary>文本查询过滤</summary>
    [JsonPropertyName("query")]
    public string? Query { get; init; }

    /// <summary>时间排序方向：<c>asc</c> 或 <c>desc</c></summary>
    [JsonPropertyName("sortDir")]
    public string? SortDir { get; init; }
}
