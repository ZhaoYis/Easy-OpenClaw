using System.Text.Json.Serialization;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 可 JSON 序列化的单条 Claim，用于 <see cref="OpenClawSignalRPrincipalSnapshot"/> 与 Hybrid/分布式连接快照。
/// </summary>
/// <param name="Type">Claim 类型</param>
/// <param name="Value">Claim 值</param>
/// <param name="ValueType">值类型 URI，可为 null</param>
/// <param name="Issuer">颁发者</param>
/// <param name="OriginalIssuer">原始颁发者</param>
/// <param name="Properties">附加属性字典，可选</param>
public sealed record OpenClawSignalRClaimSnapshot(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("valueType")]
    string? ValueType,
    [property: JsonPropertyName("issuer")] string? Issuer,
    [property: JsonPropertyName("originalIssuer")]
    string? OriginalIssuer,
    [property: JsonPropertyName("properties")]
    Dictionary<string, string>? Properties = null);