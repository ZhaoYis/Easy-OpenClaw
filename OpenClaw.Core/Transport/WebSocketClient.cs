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

    /// <summary>
    /// 收到一条完整文本消息时触发；参数为 UTF-8 解码后的 JSON 字符串。
    /// 用法：由 <see cref="GatewayClient"/> 订阅并解析为 response / event 帧。
    /// </summary>
    public event Func<string, Task>? OnMessage;

    /// <summary>
    /// 对端关闭或本地检测到 Close 帧时触发；参数为关闭状态码与描述（可能为 null）。
    /// </summary>
    public event Func<WebSocketCloseStatus?, string?, Task>? OnClosed;

    /// <summary>
    /// 接收循环中发生 <see cref="WebSocketException"/> 等错误时触发。
    /// </summary>
    public event Func<Exception, Task>? OnError;

    /// <summary>当前底层套接字是否处于 Open 状态；可在测试中覆写以模拟已连接。</summary>
    public virtual bool IsConnected =>
        _ws?.State == WebSocketState.Open;

    /// <summary>
    /// 创建客户端；可选传入 TLS 证书 SHA-256 指纹（十六进制，可含冒号）以启用证书固定。
    /// </summary>
    /// <param name="uri">WebSocket 地址（通常为 <c>wss://</c>）</param>
    /// <param name="tlsFingerprint">证书指纹；null 或空白表示不固定证书，使用系统默认校验</param>
    public WebSocketClient(Uri uri, string? tlsFingerprint = null)
    {
        _uri = uri;
        _pinnedFingerprint = NormalizeFingerprint(tlsFingerprint);
    }

    /// <summary>
    /// 建立连接并启动后台接收循环；重复调用会先释放旧连接。
    /// 设置 Origin/User-Agent/KeepAlive；若配置了指纹则覆盖远程证书校验回调。
    /// </summary>
    /// <param name="ct">可取消连接握手</param>
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

    /// <summary>
    /// 向对方发送 NormalClosure 并等待接收任务结束；用于优雅关闭。
    /// </summary>
    /// <param name="ct">取消关闭握手</param>
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

    /// <summary>
    /// 持续 <see cref="ClientWebSocket.ReceiveAsync"/> 直到取消、关闭或错误；文本消息合并后触发 <see cref="OnMessage"/>。
    /// </summary>
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

    /// <summary>
    /// 调用 <see cref="CloseAsync"/> 并释放底层资源；可多次调用。
    /// </summary>
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