using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Transport;

/// <summary>
/// Low-level WebSocket transport: connect, send text frames, receive loop.
/// </summary>
public sealed class WebSocketClient : IAsyncDisposable
{
    private readonly Uri _uri;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    public event Func<string, Task>? OnMessage;
    public event Func<WebSocketCloseStatus?, string?, Task>? OnClosed;
    public event Func<Exception, Task>? OnError;

    public bool IsConnected =>
        _ws?.State == WebSocketState.Open;

    public WebSocketClient(Uri uri)
    {
        _uri = uri;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _ws?.Dispose();
        _ws = new ClientWebSocket();
        _ws.Options.SetRequestHeader("Origin", GatewayConstants.Transport.Origin);
        _ws.Options.SetRequestHeader("User-Agent", GatewayConstants.Transport.DefaultUserAgent);
        _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(GatewayConstants.Transport.KeepAliveIntervalSeconds);

        await _ws.ConnectAsync(_uri, ct);

        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);
    }

    public async Task SendAsync(string json, CancellationToken ct = default)
    {
        if (_ws is null || _ws.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket is not connected.");

        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken: ct);
    }

    public async Task CloseAsync(CancellationToken ct = default)
    {
        if (_ws is { State: WebSocketState.Open or WebSocketState.CloseReceived })
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, GatewayConstants.Transport.CloseDescription, ct);
            }
            catch
            {
                // best-effort
            }
        }

        if (_receiveCts is not null)
            await _receiveCts.CancelAsync();
        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask;
            }
            catch
            {
                /* swallow */
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(GatewayConstants.Transport.ReceiveBufferSize);
        try
        {
            while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (OnClosed is not null)
                            await OnClosed(result.CloseStatus, result.CloseStatusDescription);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                    if (OnMessage is not null)
                        await OnMessage(json);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // graceful shutdown
        }
        catch (WebSocketException ex)
        {
            if (OnError is not null)
                await OnError(ex);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _receiveCts?.Dispose();
        _ws?.Dispose();
    }
}