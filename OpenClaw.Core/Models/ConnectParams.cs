using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// connect RPC 方法的请求参数，用于完成网关握手认证。
/// </summary>
public sealed record ConnectParams
{
    /// <summary>客户端支持的最低协议版本</summary>
    [JsonPropertyName("minProtocol")] public int MinProtocol { get; init; } = 3;

    /// <summary>客户端支持的最高协议版本</summary>
    [JsonPropertyName("maxProtocol")] public int MaxProtocol { get; init; } = 3;

    /// <summary>客户端基本信息（标识、版本、平台、模式）</summary>
    [JsonPropertyName("client")] public required ClientInfo Client { get; init; }

    /// <summary>请求的连接角色（如 "operator"、"node"）</summary>
    [JsonPropertyName("role")] public string Role { get; init; } = GatewayConstants.Roles.Operator;

    /// <summary>请求的权限作用域列表</summary>
    [JsonPropertyName("scopes")] public string[] Scopes { get; init; } = [GatewayConstants.Scopes.Admin, GatewayConstants.Scopes.Approvals, GatewayConstants.Scopes.Pairing];

    /// <summary>设备身份信息（公钥、签名等 Ed25519 认证数据）</summary>
    [JsonPropertyName("device")] public required DeviceInfo Device { get; init; }

    /// <summary>客户端声明的能力列表（如 "tool-events"）</summary>
    [JsonPropertyName("caps")] public string[] Caps { get; init; } = ["tool-events"];

    /// <summary>认证凭据（Gateway Token 及可选的 DeviceToken）</summary>
    [JsonPropertyName("auth")] public required AuthInfo Auth { get; init; }

    /// <summary>客户端 User-Agent 字符串</summary>
    [JsonPropertyName("userAgent")] public string? UserAgent { get; init; }

    /// <summary>客户端语言/区域设置</summary>
    [JsonPropertyName("locale")] public string Locale { get; init; } = "zh";
}

/// <summary>
/// 连接握手中的客户端基本信息。
/// </summary>
public sealed record ClientInfo
{
    /// <summary>客户端标识符（如 "cli"、"webchat-ui"）</summary>
    [JsonPropertyName("id")] public string Id { get; init; } = GatewayConstants.ClientIds.Cli;

    /// <summary>客户端版本号</summary>
    [JsonPropertyName("version")] public string Version { get; init; } = GatewayConstants.DefaultClientVersion;

    /// <summary>运行平台标识（如 "MacIntel"、"Win32"、"dotnet"）</summary>
    [JsonPropertyName("platform")] public string Platform { get; init; } = "dotnet";

    /// <summary>客户端运行模式（如 "cli"、"ui"、"backend"）</summary>
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

    /// <summary>服务端在 connect.challenge 中下发的随机数，参与签名计算</summary>
    [JsonPropertyName("nonce")] public required string Nonce { get; init; }
}
