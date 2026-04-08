using OpenClaw.Core.Transport;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="WebSocketClient"/> 的基础行为测试（未建立真实连接时的错误路径与释放）。
/// </summary>
public sealed class WebSocketClientTests
{
    /// <summary>
    /// 未连接时调用 <see cref="WebSocketClient.SendAsync"/> 应抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    [Fact]
    public async Task SendAsync_throws_when_socket_not_open()
    {
        await using var ws = new WebSocketClient(new Uri("ws://127.0.0.1:59998"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ws.SendAsync("{}"));
    }

    /// <summary>
    /// 未调用 <see cref="WebSocketClient.ConnectAsync"/> 时 <see cref="WebSocketClient.DisposeAsync"/> 仍应安全完成。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_without_connect_completes()
    {
        await using var ws = new WebSocketClient(new Uri("ws://127.0.0.1:59997"));
        await ws.DisposeAsync();
    }

    // ── TLS Fingerprint Pinning ──────────────────────────────

    [Fact]
    public void NormalizeFingerprint_null_returns_null()
    {
        Assert.Null(WebSocketClient.NormalizeFingerprint(null));
        Assert.Null(WebSocketClient.NormalizeFingerprint(""));
        Assert.Null(WebSocketClient.NormalizeFingerprint("   "));
    }

    [Fact]
    public void NormalizeFingerprint_pure_hex()
    {
        var hex = "A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2";
        var result = WebSocketClient.NormalizeFingerprint(hex);
        Assert.NotNull(result);
        Assert.Equal(32, result!.Length);
        Assert.Equal(0xA1, result[0]);
        Assert.Equal(0xB2, result[^1]);
    }

    [Fact]
    public void NormalizeFingerprint_colon_separated()
    {
        var fp = "A1:B2:C3:D4:E5:F6:A1:B2:C3:D4:E5:F6:A1:B2:C3:D4:E5:F6:A1:B2:C3:D4:E5:F6:A1:B2:C3:D4:E5:F6:A1:B2";
        var result = WebSocketClient.NormalizeFingerprint(fp);
        Assert.NotNull(result);
        Assert.Equal(32, result!.Length);
        Assert.Equal(0xA1, result[0]);
    }

    [Fact]
    public void NormalizeFingerprint_with_spaces()
    {
        var fp = " A1B2C3D4 E5F6A1B2 C3D4E5F6 A1B2C3D4 E5F6A1B2 C3D4E5F6 A1B2C3D4 E5F6A1B2 ";
        var result = WebSocketClient.NormalizeFingerprint(fp);
        Assert.NotNull(result);
        Assert.Equal(32, result!.Length);
    }

    [Fact]
    public async Task Constructor_with_fingerprint_does_not_throw()
    {
        var fp = "A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2";
        await using var ws = new WebSocketClient(new Uri("wss://example.com"), fp);
        Assert.False(ws.IsConnected);
    }

    [Fact]
    public async Task Constructor_without_fingerprint_does_not_throw()
    {
        await using var ws = new WebSocketClient(new Uri("wss://example.com"));
        Assert.False(ws.IsConnected);
    }

    [Fact]
    public void NormalizeFingerprint_invalid_hex_throws()
    {
        Assert.Throws<FormatException>(() => WebSocketClient.NormalizeFingerprint("ZZZZ"));
    }
}
