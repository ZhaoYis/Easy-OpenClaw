using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// <see cref="GatewayConstants.Methods.Connect"/> RPC 方法的请求参数，用于完成网关握手认证。
/// </summary>
public sealed record ConnectParams
{
    /// <summary>
    /// 客户端支持的最低协议版本。
    /// <see cref="GatewayConstants.Protocol.Version"/>
    /// </summary>
    [JsonPropertyName("minProtocol")] public int MinProtocol { get; init; } = GatewayConstants.Protocol.Version;

    /// <summary>
    /// 客户端支持的最高协议版本。
    /// <see cref="GatewayConstants.Protocol.Version"/>
    /// </summary>
    [JsonPropertyName("maxProtocol")] public int MaxProtocol { get; init; } = GatewayConstants.Protocol.Version;

    /// <summary>客户端基本信息（标识、版本、平台、模式）</summary>
    [JsonPropertyName("client")] public required ClientInfo Client { get; init; }

    /// <summary>
    /// 请求的连接角色。
    /// <see cref="GatewayConstants.Roles"/>
    /// </summary>
    [JsonPropertyName("role")] public string Role { get; init; } = GatewayConstants.Roles.Operator;

    /// <summary>
    /// 请求的权限作用域列表。
    /// <see cref="GatewayConstants.Scopes"/>
    /// </summary>
    [JsonPropertyName("scopes")] public string[] Scopes { get; init; } = [GatewayConstants.Scopes.Admin, GatewayConstants.Scopes.Approvals, GatewayConstants.Scopes.Pairing];

    /// <summary>设备身份信息（公钥、签名等 Ed25519 认证数据）</summary>
    [JsonPropertyName("device")] public required DeviceInfo Device { get; init; }

    /// <summary>
    /// 客户端声明的能力列表。
    /// <see cref="GatewayConstants.Protocol.CapToolEvents"/>
    /// </summary>
    [JsonPropertyName("caps")] public string[] Caps { get; init; } = [GatewayConstants.Protocol.CapToolEvents];

    /// <summary>
    /// 节点角色的命令允许列表（command allowlist for invoke）。
    /// 仅在 <see cref="Role"/> 为 <see cref="GatewayConstants.Roles.Node"/> 时由网关校验。
    /// </summary>
    [JsonPropertyName("commands")] public string[] Commands { get; init; } = [];

    /// <summary>
    /// 节点角色的粒度权限开关（如 <c>"camera.capture": true</c>, <c>"screen.record": false</c>）。
    /// 仅在 <see cref="Role"/> 为 <see cref="GatewayConstants.Roles.Node"/> 时由网关校验。
    /// </summary>
    [JsonPropertyName("permissions")] public Dictionary<string, bool>? Permissions { get; init; }

    /// <summary>认证凭据（Gateway Token 及可选的 DeviceToken）</summary>
    [JsonPropertyName("auth")] public required AuthInfo Auth { get; init; }

    /// <summary>
    /// 客户端 User-Agent 字符串。
    /// <see cref="GatewayConstants.Transport.UserAgentTemplate"/>
    /// </summary>
    [JsonPropertyName("userAgent")] public string? UserAgent { get; init; }

    /// <summary>
    /// 客户端语言/区域设置。
    /// <see cref="GatewayConstants.Defaults.Locale"/>
    /// </summary>
    [JsonPropertyName("locale")] public string Locale { get; init; } = GatewayConstants.Defaults.Locale;
}

/// <summary>
/// 连接握手中的客户端基本信息。
/// </summary>
public sealed record ClientInfo
{
    /// <summary>
    /// 客户端标识符。
    /// <see cref="GatewayConstants.ClientIds"/>
    /// </summary>
    [JsonPropertyName("id")] public string Id { get; init; } = GatewayConstants.ClientIds.Cli;

    /// <summary>
    /// 客户端版本号。
    /// <see cref="GatewayConstants.DefaultClientVersion"/>
    /// </summary>
    [JsonPropertyName("version")] public string Version { get; init; } = GatewayConstants.DefaultClientVersion;

    /// <summary>
    /// 运行平台标识。
    /// <see cref="GatewayConstants.Platforms"/>
    /// </summary>
    [JsonPropertyName("platform")] public string Platform { get; init; } = GatewayConstants.Platforms.DotNet;

    /// <summary>
    /// 客户端运行模式。
    /// <see cref="GatewayConstants.ClientModes"/>
    /// </summary>
    [JsonPropertyName("mode")] public string Mode { get; init; } = GatewayConstants.ClientModes.Cli;

    /// <summary>本次运行的实例唯一 ID（每次启动自动生成）</summary>
    [JsonPropertyName("instanceId")] public string InstanceId { get; init; } = Guid.NewGuid().ToString();
}

/// <summary>
/// 连接握手中的认证凭据信息。
/// </summary>
public sealed record AuthInfo
{
    /// <summary>Gateway 访问令牌（在网关配置中设定）</summary>
    [JsonPropertyName("token")] public string? Token { get; init; }

    /// <summary>Gateway 访问密码（与 Token 二选一，取决于网关 auth mode 配置）</summary>
    [JsonPropertyName("password")] public string? Password { get; init; }

    /// <summary>设备令牌（首次连接后由网关签发，后续重连时携带可跳过配对审批）</summary>
    [JsonPropertyName("deviceToken")] public string? DeviceToken { get; init; }
}

/// <summary>
/// 连接握手中的设备身份信息，携带 Ed25519 签名用于证明设备身份。
/// </summary>
public sealed record DeviceInfo
{
    /// <summary>设备 ID（SHA-256(公钥) 的十六进制小写表示）</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Ed25519 公钥（32 字节原始值的 base64url 无填充编码）</summary>
    [JsonPropertyName("publicKey")] public required string PublicKey { get; init; }

    /// <summary>Ed25519 签名（64 字节原始值的 base64url 无填充编码）</summary>
    [JsonPropertyName("signature")] public required string Signature { get; init; }

    /// <summary>签名生成时间戳（Unix 毫秒），用于防重放</summary>
    [JsonPropertyName("signedAt")] public required long SignedAt { get; init; }

    /// <summary>
    /// 服务端在 <see cref="GatewayConstants.Events.ConnectChallenge"/> 中下发的随机数，参与签名计算
    /// </summary>
    [JsonPropertyName("nonce")] public required string Nonce { get; init; }
}

/// <summary>
/// 认证失败时 <c>error.details</c> 中的结构化信息，包含错误码和恢复提示。
/// 同时承载 <c>DEVICE_AUTH_*</c> 迁移诊断码的 <see cref="Reason"/> 字段。
/// </summary>
public sealed record AuthErrorDetails
{
    /// <summary>认证错误码（如 <see cref="GatewayConstants.ErrorCodes.AuthTokenMismatch"/> 或 <c>DEVICE_AUTH_*</c>）</summary>
    [JsonPropertyName("code")] public string? Code { get; init; }

    /// <summary>
    /// 稳定的诊断原因标识符，与 <c>DEVICE_AUTH_*</c> 码配对使用。
    /// <see cref="GatewayConstants.DeviceAuthReasons"/>
    /// </summary>
    [JsonPropertyName("reason")] public string? Reason { get; init; }

    /// <summary>是否可使用缓存的 per-device token 重试</summary>
    [JsonPropertyName("canRetryWithDeviceToken")] public bool? CanRetryWithDeviceToken { get; init; }

    /// <summary>
    /// 建议的下一步操作。
    /// <see cref="GatewayConstants.AuthRecoveryHints"/>
    /// </summary>
    [JsonPropertyName("recommendedNextStep")] public string? RecommendedNextStep { get; init; }

    /// <summary>
    /// 判断此错误是否属于设备认证迁移诊断类（<c>DEVICE_AUTH_*</c> 前缀）。
    /// </summary>
    public bool IsDeviceAuthError =>
        Code is not null && Code.StartsWith(GatewayConstants.ErrorCodes.DeviceAuthPrefix, StringComparison.Ordinal);
}
