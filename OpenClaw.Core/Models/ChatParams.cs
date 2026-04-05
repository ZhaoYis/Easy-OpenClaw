using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

public sealed record ChatSendParams
{
    [JsonPropertyName("sessionKey")]
    public required string SessionKey { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("deliver")]
    public bool Deliver { get; init; } = false;

    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; init; } = Guid.NewGuid().ToString();
}
