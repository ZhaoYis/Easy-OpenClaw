using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// <see cref="GatewayConstants.Methods.CronList"/> RPC 的可选过滤与分页参数。
/// 与上游 <c>CronListParamsSchema</c> 对齐，用于筛选、排序定时任务列表。
/// </summary>
public sealed record CronListParams
{
    /// <summary>是否在结果中包含已禁用的任务</summary>
    [JsonPropertyName("includeDisabled")]
    public bool? IncludeDisabled { get; init; }

    /// <summary>分页大小，1–200</summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>分页偏移</summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; init; }

    /// <summary>按名称等字段过滤的查询字符串</summary>
    [JsonPropertyName("query")]
    public string? Query { get; init; }

    /// <summary>按启用状态过滤：<c>all</c>、<c>enabled</c>、<c>disabled</c></summary>
    [JsonPropertyName("enabled")]
    public string? Enabled { get; init; }

    /// <summary>排序字段：<c>nextRunAtMs</c>、<c>updatedAtMs</c>、<c>name</c></summary>
    [JsonPropertyName("sortBy")]
    public string? SortBy { get; init; }

    /// <summary>排序方向：<c>asc</c> 或 <c>desc</c></summary>
    [JsonPropertyName("sortDir")]
    public string? SortDir { get; init; }
}
