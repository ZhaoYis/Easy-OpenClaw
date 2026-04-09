namespace OpenClaw.Core.Models;

/// <summary>
/// GatewayClient 的连接与行为配置选项。
/// 遵循标准 Options 模式，通过 <c>IOptions&lt;GatewayOptions&gt;</c> 注入。
/// </summary>
public sealed class GatewayOptions
{
    /// <summary>配置节名称，用于从 IConfiguration 中绑定</summary>
    public const string SectionName = "OpenClaw";

    /// <summary>
    /// Gateway WebSocket 地址。
    /// <see cref="GatewayConstants.DefaultGatewayUrl"/>
    /// </summary>
    public string Url { get; set; } = GatewayConstants.DefaultGatewayUrl;

    /// <summary>Gateway 访问令牌，需与网关配置中的 token 一致</summary>
    public string Token { get; set; } = "";

    /// <summary>Gateway 访问密码，与 Token 二选一（取决于网关 auth mode 配置）</summary>
    public string? Password { get; set; }

    /// <summary>
    /// 可选的网关 TLS 证书 SHA-256 指纹（hex 编码，冒号分隔或纯 hex 均可），
    /// 用于证书固定（certificate pinning）。设置后客户端将仅接受指纹匹配的服务端证书。
    /// 对应 CLI <c>--tls-fingerprint</c> 或配置 <c>gateway.remote.tlsFingerprint</c>。
    /// </summary>
    public string? TlsFingerprint { get; set; }

    /// <summary>
    /// Ed25519 设备私钥种子的持久化文件路径（hex 编码），为 null 则使用临时密钥。
    /// <see cref="GatewayConstants.FileNames.DeviceKey"/>
    /// </summary>
    public string? KeyFilePath { get; set; }

    /// <summary>
    /// 网关签发的 DeviceToken 持久化文件路径，用于免审批重连。
    /// <see cref="GatewayConstants.FileNames.DeviceToken"/>
    /// </summary>
    public string? DeviceTokenFilePath { get; set; }

    /// <summary>
    /// 服务端授予的 scope 集合缓存文件路径，重连时复用已授予的作用域。
    /// <see cref="GatewayConstants.FileNames.DeviceScopes"/>
    /// </summary>
    public string? DeviceScopesFilePath { get; set; }

    /// <summary>
    /// 客户端标识符。
    /// <see cref="GatewayConstants.ClientIds"/>
    /// </summary>
    public string ClientId { get; set; } = GatewayConstants.ClientIds.Cli;

    /// <summary>
    /// 客户端版本号。
    /// <see cref="GatewayConstants.DefaultClientVersion"/>
    /// </summary>
    public string ClientVersion { get; set; } = GatewayConstants.DefaultClientVersion;

    /// <summary>
    /// 客户端运行模式。
    /// <see cref="GatewayConstants.ClientModes"/>
    /// </summary>
    public string ClientMode { get; set; } = GatewayConstants.ClientModes.Cli;

    /// <summary>
    /// 连接角色。
    /// <see cref="GatewayConstants.Roles"/>
    /// </summary>
    public string Role { get; set; } = GatewayConstants.Roles.Operator;

    /// <summary>
    /// 请求的权限作用域列表。
    /// <see cref="GatewayConstants.Scopes"/>
    /// </summary>
    public string[] Scopes { get; set; } = [GatewayConstants.Scopes.Admin, GatewayConstants.Scopes.Approvals, GatewayConstants.Scopes.Pairing];

    /// <summary>断线重连的等待间隔</summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>断线重连最大尝试次数</summary>
    public int MaxReconnectAttempts { get; set; } = 10;

    /// <summary>单次 RPC 请求的超时时间</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// <see cref="GatewayConstants.ErrorCodes.NotPaired"/> 状态下轮询重试的初始间隔
    /// </summary>
    public TimeSpan PairingRetryDelay { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// <see cref="GatewayConstants.ErrorCodes.NotPaired"/> 状态下轮询重试的最大间隔（指数退避上限）
    /// </summary>
    public TimeSpan PairingRetryMaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// <see cref="GatewayConstants.ErrorCodes.NotPaired"/> 状态下的最大重试次数，0 表示无限重试
    /// </summary>
    public int MaxPairingRetries { get; set; }

    // ─── Health Monitor ──────────────────────────────────

    /// <summary>是否启用后台健康监控服务，默认关闭</summary>
    public bool EnableHealthMonitor { get; set; }

    /// <summary>主动健康探测的轮询间隔（秒）</summary>
    public int HealthPollIntervalSeconds { get; set; } = 30;

    /// <summary>tick 事件超时阈值（秒），超过此时间未收到 tick 则视为不健康</summary>
    public int TickTimeoutSeconds { get; set; } = 60;

    /// <summary>heartbeat 事件超时阈值（秒），超过此时间未收到 heartbeat 则视为不健康</summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 120;
}
