using System.Net.WebSockets;
using OpenClaw.Core.Tests.Support;
using OpenClaw.Core.Transport;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="WebSocketClient"/> 与真实 Kestrel WebSocket 服务端之间的集成测试（非桩），
/// 覆盖 <see cref="WebSocketClient.ConnectAsync"/>、<see cref="WebSocketClient.SendAsync"/>、
/// 接收循环、<see cref="WebSocketClient.CloseAsync"/> 与 <see cref="WebSocketClient.DisposeAsync"/>。
/// </summary>
[Trait("Category", "Integration")]
public sealed class WebSocketClientIntegrationTests
{
    /// <summary>
    /// 再次调用 <see cref="WebSocketClient.ConnectAsync"/> 时应释放旧套接字并建立新连接（覆盖 <c>_ws?.Dispose()</c> 分支）。
    /// </summary>
    [Fact]
    public async Task ConnectAsync_disposes_previous_socket_when_called_twice()
    {
        await using var server = await WebSocketTestServer.StartAsync();
        await using var client = new WebSocketClient(server.EchoWebSocketUri);
        await client.ConnectAsync();
        await client.ConnectAsync();
        Assert.True(client.IsConnected);
    }

    /// <summary>
    /// 连接回显端点后发送文本，应在 <see cref="WebSocketClient.OnMessage"/> 收到相同内容。
    /// </summary>
    [Fact]
    public async Task Connect_send_text_receives_echo()
    {
        await using var server = await WebSocketTestServer.StartAsync();
        await using var client = new WebSocketClient(server.EchoWebSocketUri);

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.OnMessage += json =>
        {
            tcs.TrySetResult(json);
            return Task.CompletedTask;
        };

        await client.ConnectAsync();
        Assert.True(client.IsConnected);

        const string payload = """{"integration":true,"n":1}""";
        await client.SendAsync(payload);

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(payload, received);
    }

    /// <summary>
    /// 服务端先发二进制帧再关闭时，客户端应跳过文本处理并最终触发 <see cref="WebSocketClient.OnClosed"/>。
    /// </summary>
    [Fact]
    public async Task Binary_frame_then_server_close_triggers_OnClosed()
    {
        await using var server = await WebSocketTestServer.StartAsync();
        await using var client = new WebSocketClient(server.BinaryThenCloseWebSocketUri);

        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.OnClosed += (_, _) =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        await client.ConnectAsync();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// 服务端主动关闭时，客户端应触发 <see cref="WebSocketClient.OnClosed"/>。
    /// </summary>
    [Fact]
    public async Task Server_close_triggers_OnClosed()
    {
        await using var server = await WebSocketTestServer.StartAsync();
        await using var client = new WebSocketClient(server.CloseImmediatelyWebSocketUri);

        var closed = new TaskCompletionSource<(WebSocketCloseStatus? Status, string? Desc)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.OnClosed += (status, desc) =>
        {
            closed.TrySetResult((status, desc));
            return Task.CompletedTask;
        };

        await client.ConnectAsync();
        var (status, desc) = await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(WebSocketCloseStatus.NormalClosure, status);
        Assert.NotNull(desc);
    }

    /// <summary>
    /// <see cref="WebSocketClient.CloseAsync"/> 后套接字不再处于 Open；随后 <see cref="WebSocketClient.DisposeAsync"/> 完成资源释放。
    /// </summary>
    [Fact]
    public async Task CloseAsync_then_DisposeAsync_releases_resources()
    {
        await using var server = await WebSocketTestServer.StartAsync();
        await using var client = new WebSocketClient(server.EchoWebSocketUri);
        await client.ConnectAsync();
        await client.CloseAsync();
        Assert.False(client.IsConnected);
        // 由末尾 await using 隐式调用 DisposeAsync
    }
}
