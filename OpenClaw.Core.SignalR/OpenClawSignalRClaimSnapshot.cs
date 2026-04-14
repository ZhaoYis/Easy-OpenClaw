using System.Text.Json.Serialization;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 可 JSON 序列化的单条 Claim，用于 <see cref="OpenClawSignalRPrincipalSnapshot"/> 与 Hybrid/分布式连接快照。
/// </summary>
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