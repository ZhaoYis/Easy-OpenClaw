using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// <see cref="GatewayConstants.Methods.SkillsSearch"/> RPC 的请求参数。
/// 用于在 ClawHub 上按关键词检索技能发现元数据（operator.read）。
/// </summary>
public sealed record SkillsSearchParams
{
    /// <summary>
    /// 搜索关键词；省略时返回默认分页结果。
    /// </summary>
    [JsonPropertyName("query")]
    public string? Query { get; init; }

    /// <summary>
    /// 返回条数上限，合法范围 1–100；省略时由网关决定。
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }
}
