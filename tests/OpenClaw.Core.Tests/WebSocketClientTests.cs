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
}
