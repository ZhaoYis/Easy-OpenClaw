using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// <see cref="GatewayConstants.Methods.LogsTail"/> RPC 方法的请求参数。
/// 用于拉取网关文件日志的尾部内容，支持游标分页和字节数限制。
/// </summary>
public sealed record LogsTailParams
{
    /// <summary>
    /// 返回的最大日志行数。默认 50 行。
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; init; } = 50;

    /// <summary>
    /// 分页游标，用于从指定位置继续拉取。
    /// 首次请求传 null，后续使用上一次响应中返回的 cursor 值实现向前翻页。
    /// </summary>
    [JsonPropertyName("cursor")]
    public string? Cursor { get; init; }

    /// <summary>
    /// 单次响应允许返回的最大字节数。
    /// 当日志行内容过大时，网关会在到达此阈值后截断返回，防止超大响应阻塞客户端。
    /// 为 null 时使用网关默认限制。
    /// </summary>
    [JsonPropertyName("maxBytes")]
    public int? MaxBytes { get; init; }
}
