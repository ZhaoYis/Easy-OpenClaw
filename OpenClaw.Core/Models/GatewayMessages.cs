using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

public abstract record GatewayFrame
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

public sealed record GatewayRequest : GatewayFrame
{
    [JsonPropertyName("type")]
    public override string Type => "req";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}

public sealed record GatewayResponse
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "res";

    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    [JsonPropertyName("error")]
    public JsonElement? Error { get; init; }
}

public sealed record GatewayEvent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "event";

    [JsonPropertyName("event")]
    public string Event { get; init; } = "";

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    [JsonPropertyName("seq")]
    public long? Seq { get; init; }

    [JsonPropertyName("stateVersion")]
    public StateVersionInfo? StateVersion { get; init; }
}

public sealed record RawFrame
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("method")]
    public string? Method { get; init; }

    [JsonPropertyName("ok")]
    public bool? Ok { get; init; }

    [JsonPropertyName("event")]
    public string? Event { get; init; }

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    [JsonPropertyName("error")]
    public JsonElement? Error { get; init; }

    [JsonPropertyName("seq")]
    public long? Seq { get; init; }

    [JsonPropertyName("stateVersion")]
    public StateVersionInfo? StateVersion { get; init; }

    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}
