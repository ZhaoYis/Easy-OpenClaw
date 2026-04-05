using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

public sealed record ConnectParams
{
    [JsonPropertyName("minProtocol")] public int MinProtocol { get; init; } = 3;
    [JsonPropertyName("maxProtocol")] public int MaxProtocol { get; init; } = 3;
    [JsonPropertyName("client")] public required ClientInfo Client { get; init; }
    [JsonPropertyName("role")] public string Role { get; init; } = GatewayConstants.Roles.Operator;
    [JsonPropertyName("scopes")] public string[] Scopes { get; init; } = [GatewayConstants.Scopes.Admin, GatewayConstants.Scopes.Approvals, GatewayConstants.Scopes.Pairing];
    [JsonPropertyName("device")] public required DeviceInfo Device { get; init; }
    [JsonPropertyName("caps")] public string[] Caps { get; init; } = ["tool-events"];
    [JsonPropertyName("auth")] public required AuthInfo Auth { get; init; }
    [JsonPropertyName("userAgent")] public string? UserAgent { get; init; }
    [JsonPropertyName("locale")] public string Locale { get; init; } = "zh";
}

public sealed record ClientInfo
{
    [JsonPropertyName("id")] public string Id { get; init; } = GatewayConstants.ClientIds.Cli;
    [JsonPropertyName("version")] public string Version { get; init; } = GatewayConstants.DefaultClientVersion;
    [JsonPropertyName("platform")] public string Platform { get; init; } = "dotnet";
    [JsonPropertyName("mode")] public string Mode { get; init; } = GatewayConstants.ClientModes.Cli;
    [JsonPropertyName("instanceId")] public string InstanceId { get; init; } = Guid.NewGuid().ToString();
}

public sealed record AuthInfo
{
    [JsonPropertyName("token")] public string? Token { get; init; }
    [JsonPropertyName("deviceToken")] public string? DeviceToken { get; init; }
}

public sealed record DeviceInfo
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("publicKey")] public required string PublicKey { get; init; }
    [JsonPropertyName("signature")] public required string Signature { get; init; }
    [JsonPropertyName("signedAt")] public required long SignedAt { get; init; }
    [JsonPropertyName("nonce")] public required string Nonce { get; init; }
}
