using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// <see cref="GatewayConstants.Methods.ToolsCatalog"/> RPC 的可选请求参数。
/// 用于指定要查询工具目录的 Agent，以及是否包含插件工具。
/// </summary>
public sealed record ToolsCatalogParams
{
    /// <summary>
    /// 目标 Agent ID；省略时使用网关默认 Agent。
    /// </summary>
    [JsonPropertyName("agentId")]
    public string? AgentId { get; init; }

    /// <summary>
    /// 是否在目录中包含插件注册的工具；省略时由网关使用默认策略。
    /// </summary>
    [JsonPropertyName("includePlugins")]
    public bool? IncludePlugins { get; init; }
}
