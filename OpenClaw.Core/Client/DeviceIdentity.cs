using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// Manages an Ed25519 keypair for gateway device authentication.
/// Handles key generation, persistence, device-ID derivation, and challenge signing.
///
/// Encoding conventions (matching the Control UI / JS client):
///   - device.id       → SHA-256(rawPublicKey) lowercase hex
///   - device.publicKey → raw 32-byte key, base64url no-padding
///   - device.signature → raw 64-byte sig, base64url no-padding
/// </summary>
public sealed class DeviceIdentity : IDisposable
{
    private static readonly SignatureAlgorithm Algorithm = SignatureAlgorithm.Ed25519;

    private readonly Key _signingKey;
    private readonly byte[] _rawPublicKey;

    public string DeviceId { get; }
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
    /// Build the v3 auth payload and produce an Ed25519 signature.
    /// v3 payload: v3|deviceId|clientId|clientMode|role|scopes|signedAtMs|token|nonce|platform|deviceFamily
    /// v2 payload (legacy): v2|deviceId|clientId|clientMode|role|scopes|signedAtMs|token|nonce
    /// </summary>
    /// <param name="clientId">客户端标识符</param>
    /// <param name="clientMode">客户端运行模式</param>
    /// <param name="role">连接角色</param>
    /// <param name="scopes">请求的权限作用域</param>
    /// <param name="token">认证令牌</param>
    /// <param name="nonce">服务端下发的随机 nonce</param>
    /// <param name="platform">运行平台标识（v3 新增绑定字段）</param>
    /// <param name="deviceFamily">设备系列标识（v3 新增绑定字段，如 "desktop"、"mobile"）</param>
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

    /// <summary>
    /// RFC 7515 base64url encoding (no padding, URL-safe alphabet).
    /// </summary>
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
