using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

public sealed record PairListResponse
{
    [JsonPropertyName("pending")]
    public PairRequest[] Pending { get; init; } = [];

    [JsonPropertyName("approved")]
    public PairRequest[] Approved { get; init; } = [];

    [JsonPropertyName("rejected")]
    public PairRequest[] Rejected { get; init; } = [];
}

public sealed record PairRequest
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = "";

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; init; } = "";

    [JsonPropertyName("clientId")]
    public string? ClientId { get; init; }

    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    [JsonPropertyName("platform")]
    public string? Platform { get; init; }

    [JsonPropertyName("ip")]
    public string? Ip { get; init; }

    [JsonPropertyName("ts")]
    public long? Ts { get; init; }
}

public sealed record PairApproveParams
{
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }
}

public sealed record PairApproveResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }
}
