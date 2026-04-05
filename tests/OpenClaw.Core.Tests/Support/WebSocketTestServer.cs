using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenClaw.Core.Tests.Support;

/// <summary>
/// 测试专用本机 Kestrel WebSocket 服务端：提供「回显」与「握手后立即关闭」两种端点，
/// 用于驱动 <see cref="OpenClaw.Core.Transport.WebSocketClient"/> 的真实连接、收发与关闭路径。
/// </summary>
internal sealed class WebSocketTestServer : IAsyncDisposable
{
    private WebApplication? _app;

    /// <summary>回显文本帧的 WebSocket 地址（路径 <c>/echo</c>）。</summary>
    public Uri EchoWebSocketUri { get; private init; } = null!;

    /// <summary>接受连接后立即向客户端发起关闭握手的地址（路径 <c>/close-now</c>）。</summary>
    public Uri CloseImmediatelyWebSocketUri { get; private init; } = null!;

    /// <summary>先发送一条二进制帧再正常关闭，用于覆盖客户端忽略非文本帧后继续读关闭帧的路径（路径 <c>/binary-then-close</c>）。</summary>
    public Uri BinaryThenCloseWebSocketUri { get; private init; } = null!;

    /// <summary>
    /// 在随机本地端口上启动 Kestrel，并注册两个 WebSocket 端点。
    /// </summary>
    /// <param name="ct">用于取消启动流程（通常传入 <see cref="CancellationToken"/> 默认值即可）</param>
    /// <returns>已启动、可获取 <see cref="EchoWebSocketUri"/> 的服务器实例</returns>
    public static async Task<WebSocketTestServer> StartAsync(CancellationToken ct = default)
    {
        var builder = WebApplication.CreateBuilder();
        // .NET 10：ListenLocalhost(0) 不允许动态端口；使用环回地址 + 0 端口由 OS 分配
        builder.WebHost.UseKestrel(static k => k.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.UseWebSockets();
        app.Map("/echo", HandleEchoAsync);
        app.Map("/close-now", HandleCloseImmediatelyAsync);
        app.Map("/binary-then-close", HandleBinaryThenCloseAsync);

        await app.StartAsync(ct).ConfigureAwait(false);

        var baseUri = ResolveHttpBaseUri(app);
        var wsHost = baseUri.Host;
        var wsPort = baseUri.Port;

        return new WebSocketTestServer
        {
            _app = app,
            EchoWebSocketUri = new Uri($"ws://{wsHost}:{wsPort}/echo"),
            CloseImmediatelyWebSocketUri = new Uri($"ws://{wsHost}:{wsPort}/close-now"),
            BinaryThenCloseWebSocketUri = new Uri($"ws://{wsHost}:{wsPort}/binary-then-close"),
        };
    }

    /// <summary>
    /// 从已启动的 <see cref="WebApplication"/> 解析 Kestrel 实际监听的 HTTP 基地址（含端口）。
    /// </summary>
    private static Uri ResolveHttpBaseUri(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var feat = server.Features.Get<IServerAddressesFeature>()
                   ?? throw new InvalidOperationException("IServerAddressesFeature 不可用。");
        var addr = feat.Addresses.FirstOrDefault(static a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException("未找到 http:// 监听地址。");
        return new Uri(addr);
    }

    /// <summary>
    /// 回显端点：循环接收完整文本消息并原样发回，直到对端关闭或出错。
    /// </summary>
    private static async Task HandleEchoAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        var buffer = new byte[8192];

        while (socket.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket
                    .ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted)
                    .ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket
                        .CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "server ack close",
                            context.RequestAborted)
                        .ConfigureAwait(false);
                    return;
                }

                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text)
                continue;

            var text = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            var outBytes = Encoding.UTF8.GetBytes(text);
            await socket
                .SendAsync(
                    new ArraySegment<byte>(outBytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    context.RequestAborted)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 发送一条非文本帧后再关闭，使客户端接收循环走「非 Text 分支」后仍能读到 Close。
    /// </summary>
    private static async Task HandleBinaryThenCloseAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        await socket
            .SendAsync(
                new ArraySegment<byte>([1, 2, 3]),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                context.RequestAborted)
            .ConfigureAwait(false);
        await socket
            .CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "after binary",
                context.RequestAborted)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 立即关闭端点：接受 WebSocket 后立刻发起服务端关闭帧，用于覆盖客户端 <see cref="OpenClaw.Core.Transport.WebSocketClient"/> 的 OnClosed 分支。
    /// </summary>
    private static async Task HandleCloseImmediatelyAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        await socket
            .CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "test immediate close",
                context.RequestAborted)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 停止 Kestrel 宿主并释放资源。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_app is null)
            return;

        await _app.StopAsync().ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
        _app = null;
    }
}
