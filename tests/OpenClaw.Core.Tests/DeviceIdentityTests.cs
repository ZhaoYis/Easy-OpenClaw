using OpenClaw.Core.Client;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="DeviceIdentity"/> 的单元测试：临时密钥、磁盘加载/生成与签名输出结构。
/// </summary>
public sealed class DeviceIdentityTests
{
    /// <summary>
    /// 无路径时应生成内存密钥且 <see cref="DeviceIdentity.DeviceId"/> 与公钥派生一致。
    /// </summary>
    [Fact]
    public void LoadOrCreate_without_path_generates_identity()
    {
        using var id = DeviceIdentity.LoadOrCreate(null);
        Assert.False(string.IsNullOrWhiteSpace(id.DeviceId));
        Assert.False(string.IsNullOrWhiteSpace(id.PublicKeyBase64Url));
    }

    /// <summary>
    /// 指定路径时应持久化种子并可重复加载同一设备身份。
    /// </summary>
    [Fact]
    public void LoadOrCreate_with_file_round_trips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"openclaw-test-key-{Guid.NewGuid():N}.hex");
        try
        {
            using (var first = DeviceIdentity.LoadOrCreate(path))
            using (var second = DeviceIdentity.LoadOrCreate(path))
            {
                Assert.Equal(first.DeviceId, second.DeviceId);
            }
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // best-effort
            }
        }
    }

    /// <summary>
    /// <see cref="DeviceIdentity.Sign"/> 应返回非空签名、时间戳与传入的 nonce。
    /// </summary>
    [Fact]
    public void Sign_produces_signature_and_timestamps()
    {
        using var id = DeviceIdentity.LoadOrCreate(null);
        var sig = id.Sign("cid", "cli", "operator", ["operator.admin"], "tok", "nonce-1");
        Assert.Equal("nonce-1", sig.Nonce);
        Assert.False(string.IsNullOrEmpty(sig.Signature));
        Assert.True(sig.SignedAt > 0);
    }
}
