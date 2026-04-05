namespace OpenClaw.Core.Models;

/// <summary>
/// GatewayClient 的连接与行为配置选项。
/// </summary>
public sealed record GatewayOptions
{
    /// <summary>Gateway WebSocket 地址（如 "ws://localhost:18789"）</summary>
    public required string Url { get; init; }

    /// <summary>Gateway 访问令牌，需与网关配置中的 token 一致</summary>
    public required string Token { get; init; }

    /// <summary>Ed25519 设备私钥种子的持久化文件路径（hex 编码），为 null 则使用临时密钥</summary>
    public string? KeyFilePath { get; init; }

    /// <summary>网关签发的 DeviceToken 持久化文件路径，用于免审批重连</summary>
    public string? DeviceTokenFilePath { get; init; }

    /// <summary>客户端标识符，默认 "cli"</summary>
    public string ClientId { get; init; } = GatewayConstants.ClientIds.Cli;

    /// <summary>客户端版本号</summary>
    public string ClientVersion { get; init; } = GatewayConstants.DefaultClientVersion;

    /// <summary>客户端运行模式，默认 "cli"</summary>
    public string ClientMode { get; init; } = GatewayConstants.ClientModes.Cli;

    /// <summary>连接角色，默认 "operator"</summary>
    public string Role { get; init; } = GatewayConstants.Roles.Operator;

    /// <summary>请求的权限作用域列表</summary>
    public string[] Scopes { get; init; } = [GatewayConstants.Scopes.Admin, GatewayConstants.Scopes.Approvals, GatewayConstants.Scopes.Pairing];

    /// <summary>断线重连的等待间隔</summary>
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>断线重连最大尝试次数</summary>
    public int MaxReconnectAttempts { get; init; } = 10;

    /// <summary>单次 RPC 请求的超时时间</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>NOT_PAIRED 状态下轮询重试的初始间隔</summary>
    public TimeSpan PairingRetryDelay { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>NOT_PAIRED 状态下轮询重试的最大间隔（指数退避上限）</summary>
    public TimeSpan PairingRetryMaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>NOT_PAIRED 状态下的最大重试次数，0 表示无限重试</summary>
    public int MaxPairingRetries { get; init; }
}
