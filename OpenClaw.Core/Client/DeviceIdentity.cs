using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;
using OpenClaw.Core.Logging;

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

    private DeviceIdentity(Key signingKey)
    {
        _signingKey = signingKey;
        _rawPublicKey = signingKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);

        PublicKeyBase64Url = Base64UrlEncode(_rawPublicKey);
        DeviceId = Convert.ToHexString(SHA256.HashData(_rawPublicKey)).ToLowerInvariant();
    }

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
    /// Build the v2 auth payload and produce an Ed25519 signature.
    /// Payload: v2|deviceId|clientId|clientMode|role|scopes|signedAtMs|token|nonce
    /// </summary>
    public DeviceSignature Sign(
        string clientId,
        string clientMode,
        string role,
        string[] scopes,
        string token,
        string nonce)
    {
        var signedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var scopesJoined = string.Join(",", scopes);

        var payload = $"v2|{DeviceId}|{clientId}|{clientMode}|{role}|{scopesJoined}|{signedAt}|{token}|{nonce}";

        Log.Debug($"签名 payload: {payload[..Math.Min(payload.Length, 80)]}...");

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

    public void Dispose()
    {
        _signingKey.Dispose();
    }
}

public sealed record DeviceSignature
{
    public required string Signature { get; init; }
    public required long SignedAt { get; init; }
    public required string Nonce { get; init; }
}
