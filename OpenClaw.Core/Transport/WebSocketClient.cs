using System.Buffers;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Transport;

/// <summary>
/// WebSocket 传输实现：连接、发送文本帧、接收循环。
/// 类非 sealed，便于单元测试中通过子类覆写 <see cref="SendAsync"/> 等成员模拟出站流量。
/// </summary>
public class WebSocketClient : IAsyncDisposable
{
    private readonly Uri _uri;
    private readonly byte[]? _pinnedFingerprint;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    public event Func<string, Task>? OnMessage;
    public event Func<WebSocketCloseStatus?, string?, Task>? OnClosed;
    public event Func<Exception, Task>? OnError;

    /// <summary>当前底层套接字是否处于 Open 状态；可在测试中覆写以模拟已连接。</summary>
    public virtual bool IsConnected =>
        _ws?.State == WebSocketState.Open;

    public WebSocketClient(Uri uri, string? tlsFingerprint = null)
    {
        _uri = uri;
        _pinnedFingerprint = NormalizeFingerprint(tlsFingerprint);
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _ws?.Dispose();
        _ws = new ClientWebSocket();
        _ws.Options.SetRequestHeader("Origin", GatewayConstants.Transport.Origin);
        _ws.Options.SetRequestHeader("User-Agent", GatewayConstants.Transport.DefaultUserAgent);
        _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(GatewayConstants.Transport.KeepAliveIntervalSeconds);

        if (_pinnedFingerprint is not null)
        {
            _ws.Options.RemoteCertificateValidationCallback = (_, cert, _, _) =>
            {
                if (cert is null) return false;
                using var x509 = new X509Certificate2(cert);
                var certHash = SHA256.HashData(x509.RawData);
                return CryptographicOperations.FixedTimeEquals(certHash, _pinnedFingerprint);
            };
        }

        await _ws.ConnectAsync(_uri, ct);

        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);
    }

    /// <summary>发送一条文本帧；可在测试中覆写以记录 JSON 并回注伪造响应。</summary>
    public virtual async Task SendAsync(string json, CancellationToken ct = default)
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

    /// <summary>
    /// 将十六进制指纹字符串（支持冒号分隔或纯 hex）规范化为 32 字节 SHA-256 哈希。
    /// </summary>
    internal static byte[]? NormalizeFingerprint(string? fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint)) return null;
        var hex = fingerprint.Replace(":", "").Replace(" ", "").Trim();
        return Convert.FromHexString(hex);
    }
}