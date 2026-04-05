namespace OpenClaw.Core.Models;

public sealed record GatewayOptions
{
    public required string Url { get; init; }
    public required string Token { get; init; }

    /// <summary>
    /// Path to persist the Ed25519 private key seed (hex).
    /// </summary>
    public string? KeyFilePath { get; init; }

    /// <summary>
    /// Path to persist the deviceToken issued by the gateway after hello-ok.
    /// </summary>
    public string? DeviceTokenFilePath { get; init; }

    public string ClientId { get; init; } = GatewayConstants.ClientIds.Cli;
    public string ClientVersion { get; init; } = GatewayConstants.DefaultClientVersion;
    public string ClientMode { get; init; } = GatewayConstants.ClientModes.Cli;
    public string Role { get; init; } = GatewayConstants.Roles.Operator;
    public string[] Scopes { get; init; } = [GatewayConstants.Scopes.Admin, GatewayConstants.Scopes.Approvals, GatewayConstants.Scopes.Pairing];

    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(3);
    public int MaxReconnectAttempts { get; init; } = 10;
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>NOT_PAIRED 轮询重试初始间隔</summary>
    public TimeSpan PairingRetryDelay { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>NOT_PAIRED 轮询重试最大间隔（指数退避上限）</summary>
    public TimeSpan PairingRetryMaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>NOT_PAIRED 最大重试次数（0 = 无限）</summary>
    public int MaxPairingRetries { get; init; }
}
