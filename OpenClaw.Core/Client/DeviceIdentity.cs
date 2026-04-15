using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 管理用于网关设备认证的 Ed25519 密钥对：生成与持久化、由公钥派生设备 ID、以及对连接挑战签名。
/// 编码约定与 Control UI / JS 客户端一致：
/// <list type="bullet">
/// <item><description><c>device.id</c> → <c>SHA-256(rawPublicKey)</c> 小写十六进制</description></item>
/// <item><description><c>device.publicKey</c> → 原始 32 字节公钥，base64url 无填充</description></item>
/// <item><description><c>device.signature</c> → 原始 64 字节签名，base64url 无填充</description></item>
/// </list>
/// </summary>
public sealed class DeviceIdentity : IDisposable
{
    private static readonly SignatureAlgorithm Algorithm = SignatureAlgorithm.Ed25519;

    private readonly Key _signingKey;
    private readonly byte[] _rawPublicKey;

    /// <summary>由公钥 SHA-256 派生的设备标识（小写十六进制），与网关 <c>device.id</c> 一致。</summary>
    public string DeviceId { get; }

    /// <summary>原始 32 字节公钥的 base64url（无填充），与网关 <c>device.publicKey</c> 一致。</summary>
    public string PublicKeyBase64Url { get; }

    /// <summary>
    /// 私有构造函数，从已有的 Ed25519 签名密钥初始化设备身份。
    /// 导出原始公钥字节，计算 base64url 编码的公钥和 SHA-256 哈希的设备 ID。
    /// </summary>
    /// <param name="signingKey">Ed25519 签名密钥（包含私钥和公钥）</param>
    private DeviceIdentity(Key signingKey)
    {
        _signingKey = signingKey;
        _rawPublicKey = signingKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);

        PublicKeyBase64Url = Base64UrlEncode(_rawPublicKey);
        DeviceId = Convert.ToHexString(SHA256.HashData(_rawPublicKey)).ToLowerInvariant();
    }

    /// <summary>
    /// 从指定路径加载已有的 Ed25519 密钥，或在密钥不存在时生成新密钥并持久化到磁盘。
    /// 密钥以十六进制编码的原始私钥种子（32 字节）形式存储在文件中。
    /// 若 <paramref name="keyFilePath"/> 为 null，则生成临时内存密钥，不写入磁盘。
    /// </summary>
    /// <param name="keyFilePath">密钥文件路径（可选）。为 null 时生成临时密钥</param>
    /// <returns>已初始化的 <see cref="DeviceIdentity"/> 实例</returns>
    public static DeviceIdentity LoadOrCreate(string? keyFilePath)
    {
        if (keyFilePath is not null && File.Exists(keyFilePath))
        {
            var hex = File.ReadAllText(keyFilePath).Trim();
            var seed = Convert.FromHexString(hex);
            var key = Key.Import(Algorithm, seed, KeyBlobFormat.RawPrivateKey,
                new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

            Log.Debug($"已加载设备密钥: {keyFilePath}");
            return new DeviceIdentity(key);
        }

        var newKey = Key.Create(Algorithm,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

        if (keyFilePath is not null)
        {
            var dir = Path.GetDirectoryName(keyFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var seed = newKey.Export(KeyBlobFormat.RawPrivateKey);
            File.WriteAllText(keyFilePath, Convert.ToHexString(seed).ToLowerInvariant());
            Log.Debug($"已生成并保存设备密钥: {keyFilePath}");
        }
        else
        {
            Log.Debug("已生成临时设备密钥（未持久化）");
        }

        return new DeviceIdentity(newKey);
    }

    /// <summary>
    /// 按 <see cref="GatewayConstants.Signature"/> 当前前缀构造 v2/v3 认证载荷并生成 Ed25519 签名。
    /// v3：<c>v3|deviceId|clientId|clientMode|role|scopes|signedAtMs|token|nonce|platform|deviceFamily</c>；
    /// v2（兼容）：<c>v2|deviceId|clientId|clientMode|role|scopes|signedAtMs|token|nonce</c>。
    /// </summary>
    /// <param name="clientId">客户端标识符</param>
    /// <param name="clientMode">客户端运行模式</param>
    /// <param name="role">连接角色</param>
    /// <param name="scopes">请求的权限作用域</param>
    /// <param name="token">参与签名的共享 token 字符串（与 connect 中 auth 一致）</param>
    /// <param name="nonce">服务端 <see cref="GatewayConstants.Events.ConnectChallenge"/> 下发的随机串</param>
    /// <param name="platform">运行平台标识（v3 绑定字段，见 <see cref="GatewayConstants.Platforms"/>）</param>
    /// <param name="deviceFamily">设备系列（如 <c>desktop</c>、<c>mobile</c>）</param>
    /// <returns>base64url 签名、签名时刻与 nonce</returns>
    public DeviceSignature Sign(
        string clientId,
        string clientMode,
        string role,
        string[] scopes,
        string token,
        string nonce,
        string platform = "",
        string deviceFamily = "")
    {
        var signedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var scopesJoined = string.Join(",", scopes);

        var versionPrefix = GatewayConstants.Signature.VersionPrefix;
        var payload = versionPrefix == GatewayConstants.Signature.V3Prefix
            ? $"{versionPrefix}|{DeviceId}|{clientId}|{clientMode}|{role}|{scopesJoined}|{signedAt}|{token}|{nonce}|{platform}|{deviceFamily}"
            : $"{versionPrefix}|{DeviceId}|{clientId}|{clientMode}|{role}|{scopesJoined}|{signedAt}|{token}|{nonce}";

        Log.Debug($"签名 payload ({versionPrefix}): {payload[..Math.Min(payload.Length, 100)]}...");

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var sig = Algorithm.Sign(_signingKey, payloadBytes);

        return new DeviceSignature
        {
            Signature = Base64UrlEncode(sig),
            SignedAt = signedAt,
            Nonce = nonce,
        };
    }

    /// <summary>RFC 4648 / JOSE 使用的 base64url：无填充、<c>-</c>/<c>_</c> 字母表。</summary>
    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// 释放底层 Ed25519 签名密钥占用的非托管资源，确保密钥材料从内存中安全清除。
    /// </summary>
    public void Dispose()
    {
        _signingKey.Dispose();
    }
}

/// <summary>
/// Ed25519 签名结果，包含 base64url 编码的签名值、签名时间戳和随机 nonce。
/// 用于网关握手阶段的设备身份验证。
/// </summary>
public sealed record DeviceSignature
{
    /// <summary>签名值，base64url 无填充编码的 64 字节 Ed25519 签名</summary>
    public required string Signature { get; init; }

    /// <summary>签名生成时的 Unix 时间戳（毫秒），用于防止重放攻击</summary>
    public required long SignedAt { get; init; }

    /// <summary>服务端下发的随机 nonce，确保每次签名唯一</summary>
    public required string Nonce { get; init; }
}
