using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;
using OpenClaw.Core.Transport;

namespace OpenClaw.Core.Client;

/// <summary>
/// High-level client that wraps WebSocket transport, request/response correlation,
/// event routing, handshake (with Ed25519 device auth), reconnection, and chat helpers.
/// </summary>
public sealed partial class GatewayClient : IAsyncDisposable
{
    private readonly GatewayOptions _options;
    private readonly RequestManager _requests;
    private readonly EventRouter _events;
    private readonly DeviceIdentity _device;
    private WebSocketClient? _ws;
    private CancellationTokenSource? _lifetimeCts;
    private int _reconnectAttempts;
    private bool _disposed;
    private volatile ConnectionState _state = ConnectionState.Disconnected;

    private string? _deviceToken;
    private HelloOkPayload? _helloOk;

    public EventRouter Events => _events;
    public bool IsConnected => _ws?.IsConnected ?? false;
    public ConnectionState State => _state;

    /// <summary>
    /// The full hello-ok payload from the last successful connect.
    /// </summary>
    public HelloOkPayload? HelloOk => _helloOk;

    /// <summary>
    /// Available RPC methods reported by the server.
    /// </summary>
    public IReadOnlyList<string> AvailableMethods => _helloOk?.Features?.Methods ?? [];

    /// <summary>
    /// Available event types reported by the server.
    /// </summary>
    public IReadOnlyList<string> AvailableEvents => _helloOk?.Features?.Events ?? [];

    public GatewayClient(GatewayOptions options)
    {
        _options = options;
        _requests = new RequestManager(options.RequestTimeout);
        _events = new EventRouter();
        _device = DeviceIdentity.LoadOrCreate(options.KeyFilePath);

        _deviceToken = LoadDeviceToken();

        Log.Info($"Device ID: {_device.DeviceId[..16]}...");
        if (_deviceToken is not null)
            Log.Debug("已加载缓存的 deviceToken");
    }

    // ─── Public API ────────────────────────────────────────

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _state = ConnectionState.Connecting;
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var challengeTcs = new TaskCompletionSource<GatewayEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        _events.On("connect.challenge", evt =>
        {
            challengeTcs.TrySetResult(evt);
            return Task.CompletedTask;
        });

        await ConnectTransportAsync(_lifetimeCts.Token);

        Log.Info("等待 connect.challenge ...");

        var challengeEvt = await challengeTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), _lifetimeCts.Token);
        Log.Success("收到 challenge");

        var nonce = "";
        if (challengeEvt.Payload.HasValue)
        {
            nonce = GetString(challengeEvt.Payload.Value, "nonce");
            var ts = GetString(challengeEvt.Payload.Value, "ts");
            Log.Debug($"  nonce={nonce}, ts={ts}");
        }

        _events.Off("connect.challenge");

        var connectParams = BuildConnectParams(nonce);
        var connectResp = await SendRequestAsync("connect", connectParams, _lifetimeCts.Token);

        if (!connectResp.Ok)
        {
            var errText = connectResp.Error?.GetRawText() ?? "unknown";
            if (IsNotPairedError(connectResp.Error))
            {
                _state = ConnectionState.WaitingForApproval;
                throw new NotPairedException(errText, connectResp.Error);
            }

            throw new InvalidOperationException($"connect 失败: {errText}");
        }

        ProcessHelloOk(connectResp);

        _state = ConnectionState.Connected;
        _reconnectAttempts = 0;
    }

    /// <summary>
    /// 带 NOT_PAIRED 自动重试的连接方法。
    /// 当设备未配对时，按指数退避轮询重连，直到外部审批服务完成批准。
    /// </summary>
    public async Task ConnectWithRetryAsync(CancellationToken ct = default)
    {
        var attempt = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await ConnectAsync(ct);
                if (attempt > 0)
                    Log.Success("审批已通过，连接成功！");
                return;
            }
            catch (NotPairedException) when (!ct.IsCancellationRequested)
            {
                attempt++;

                if (attempt == 1)
                {
                    Log.Warn("设备未配对 (NOT_PAIRED)");
                    Log.Info("等待自动审批服务批准...");
                }

                if (_options.MaxPairingRetries > 0 && attempt >= _options.MaxPairingRetries)
                {
                    Log.Error($"已达到最大配对等待次数 ({_options.MaxPairingRetries})");
                    throw;
                }

                var delay = CalculateBackoff(attempt);
                Log.Info($"第 {attempt} 次重试，{delay.TotalSeconds:F0}s 后重连...");

                await Task.Delay(delay, ct);
            }
        }
    }

    private TimeSpan CalculateBackoff(int attempt)
    {
        var baseMs = _options.PairingRetryDelay.TotalMilliseconds;
        var maxMs = _options.PairingRetryMaxDelay.TotalMilliseconds;
        var delayMs = Math.Min(baseMs * Math.Pow(1.5, attempt - 1), maxMs);
        return TimeSpan.FromMilliseconds(delayMs);
    }

    public async Task<GatewayResponse> SendRequestAsync<T>(string method, T parameters, CancellationToken ct = default)
    {
        var (id, task) = _requests.Register(_options.RequestTimeout);
        var paramsJson = JsonSerializer.SerializeToElement(parameters, JsonDefaults.SerializerOptions);

        var req = new GatewayRequest
        {
            Id = id,
            Method = method,
            Params = paramsJson,
        };

        var json = JsonSerializer.Serialize(req, JsonDefaults.SerializerOptions);
        Log.Debug($"→ req [{method}] id={id[..8]}...");

        await _ws!.SendAsync(json, ct);
        return await task;
    }

    public Task<GatewayResponse> SendRequestAsync(string method, JsonElement parameters, CancellationToken ct = default)
    {
        return SendRequestRawAsync(method, parameters, ct);
    }

    internal async Task<GatewayResponse> SendRequestRawAsync(string method, JsonElement parameters, CancellationToken ct)
    {
        var (id, task) = _requests.Register(_options.RequestTimeout);

        var req = new GatewayRequest
        {
            Id = id,
            Method = method,
            Params = parameters,
        };

        var json = JsonSerializer.Serialize(req, JsonDefaults.SerializerOptions);
        Log.Debug($"→ req [{method}] id={id[..8]}...");

        await _ws!.SendAsync(json, ct);
        return await task;
    }

    public void OnEvent(string eventName, Func<GatewayEvent, Task> handler)
        => _events.On(eventName, handler);

    public Task<GatewayResponse> ChatAsync(string userMessage, string? sessionKey = null, CancellationToken ct = default)
    {
        var key = sessionKey
                  ?? _helloOk?.Snapshot?.SessionDefaults?.MainSessionKey
                  ?? "agent:main:main";

        var param = new ChatSendParams
        {
            SessionKey = key,
            Message = userMessage,
        };
        return SendRequestAsync("chat.send", param, ct);
    }

    // ─── hello-ok Processing ───────────────────────────────

    private void ProcessHelloOk(GatewayResponse resp)
    {
        if (resp.Payload is not { } payload) return;

        _helloOk = JsonSerializer.Deserialize<HelloOkPayload>(payload.GetRawText(), JsonDefaults.SerializerOptions);
        if (_helloOk is null) return;

        Log.Success($"Handshake 完成 (type={_helloOk.Type}, protocol={_helloOk.Protocol})");

        if (_helloOk.Server is { } srv)
            Log.Info($"  Server: v{srv.Version}  connId={srv.ConnId[..8]}...");

        if (_helloOk.Features is { } feat)
            Log.Info($"  Features: {feat.Methods.Length} methods, {feat.Events.Length} events");

        if (_helloOk.Policy is { } pol)
            Log.Debug($"  Policy: tickInterval={pol.TickIntervalMs}ms, maxPayload={pol.MaxPayload / 1024 / 1024}MB");

        if (_helloOk.CanvasHostUrl is { } canvas)
            Log.Debug($"  Canvas: {canvas}");

        if (_helloOk.Auth is { } auth)
        {
            Log.Info($"  Auth: role={auth.Role}, scopes=[{string.Join(", ", auth.Scopes ?? [])}]");
            if (auth.DeviceToken is { } dt)
            {
                _deviceToken = dt;
                PersistDeviceToken(dt);
                Log.Success("  DeviceToken 已保存");
            }
        }

        if (_helloOk.Snapshot is { } snap)
            LogSnapshotHighlights(snap);
    }

    private static void LogSnapshotHighlights(SnapshotInfo snapshot)
    {
        if (snapshot.UptimeMs is { } ms)
        {
            var ts = TimeSpan.FromMilliseconds(ms);
            Log.Info($"  Gateway uptime: {ts.Days}d {ts.Hours}h {ts.Minutes}m");
        }

        if (snapshot.AuthMode is { } authMode)
            Log.Debug($"  AuthMode: {authMode}");

        if (snapshot.UpdateAvailable is { } update
            && update.LatestVersion != update.CurrentVersion)
        {
            Log.Warn($"  更新可用: {update.CurrentVersion} → {update.LatestVersion} ({update.Channel})");
        }

        if (snapshot.SessionDefaults is { } sd)
            Log.Debug($"  Default agent: {sd.DefaultAgentId}, session: {sd.MainSessionKey}");

        if (snapshot.Presence is { } presence)
            Log.Debug($"  Presence: {presence.Length} 设备在线");

        if (snapshot.Health is { } health)
        {
            var status = health.Ok ? "healthy" : "unhealthy";
            var channelCount = health.Channels?.Count ?? 0;
            var agentCount = health.Agents?.Length ?? 0;
            var sessionCount = health.Sessions?.Count ?? 0;
            Log.Debug($"  Health: {status}, {channelCount} channels, {agentCount} agents, {sessionCount} sessions");

            if (health.Channels is { } channels)
            {
                foreach (var (name, ch) in channels)
                {
                    var label = health.ChannelLabels?.GetValueOrDefault(name, name) ?? name;
                    var runState = ch.Running ? "running" : "stopped";
                    Log.Debug($"    Channel [{label}]: {runState}, configured={ch.Configured}");
                }
            }
        }

        if (snapshot.StateVersion is { } sv)
            Log.Debug($"  StateVersion: presence={sv.Presence}, health={sv.Health}");

        if (snapshot.ConfigPath is { } cfgPath)
            Log.Debug($"  Config: {cfgPath}");
    }

    // ─── DeviceToken Persistence ───────────────────────────

    private string? LoadDeviceToken()
    {
        var path = _options.DeviceTokenFilePath;
        if (path is null || !File.Exists(path)) return null;

        try
        {
            var token = File.ReadAllText(path).Trim();
            return string.IsNullOrEmpty(token) ? null : token;
        }
        catch
        {
            return null;
        }
    }

    private void PersistDeviceToken(string token)
    {
        var path = _options.DeviceTokenFilePath;
        if (path is null) return;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, token);
        }
        catch (Exception ex)
        {
            Log.Warn($"保存 deviceToken 失败: {ex.Message}");
        }
    }

    // ─── Transport ─────────────────────────────────────────

    private async Task ConnectTransportAsync(CancellationToken ct)
    {
        if (_ws is not null)
            await _ws.DisposeAsync();

        _ws = new WebSocketClient(new Uri(_options.Url));
        _ws.OnMessage += OnRawMessage;
        _ws.OnClosed += OnTransportClosed;
        _ws.OnError += OnTransportError;

        Log.Info($"连接 {_options.Url} ...");
        await _ws.ConnectAsync(ct);
        Log.Success("WebSocket 已连接");
    }

    private async Task OnRawMessage(string json)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<RawFrame>(json, JsonDefaults.SerializerOptions);
            if (raw is null) return;

            switch (raw.Type)
            {
                case "res":
                    HandleResponse(raw);
                    break;

                case "event":
                    await HandleEvent(raw);
                    break;

                default:
                    Log.Warn($"未知帧类型: {raw.Type}");
                    break;
            }
        }
        catch (JsonException ex)
        {
            Log.Error($"JSON 解析失败: {ex.Message}");
        }
    }

    private void HandleResponse(RawFrame raw)
    {
        var resp = new GatewayResponse
        {
            Id = raw.Id ?? "",
            Ok = raw.Ok ?? false,
            Payload = raw.Payload,
            Error = raw.Error,
        };
        Log.Debug($"← res id={resp.Id[..Math.Min(resp.Id.Length, 8)]}... ok={resp.Ok}");

        if (!_requests.TryComplete(resp.Id, resp))
        {
            Log.Warn($"收到未匹配的响应 id={resp.Id}");
        }
    }

    private async Task HandleEvent(RawFrame raw)
    {
        var evt = new GatewayEvent
        {
            Event = raw.Event ?? "",
            Payload = raw.Payload,
            Seq = raw.Seq,
            StateVersion = raw.StateVersion,
        };

        Log.Debug($"← event [{evt.Event}]" + (evt.Seq.HasValue ? $" seq={evt.Seq}" : ""));
        await _events.DispatchAsync(evt);
    }

    // ─── Reconnection ──────────────────────────────────────

    private async Task OnTransportClosed(System.Net.WebSockets.WebSocketCloseStatus? status, string? desc)
    {
        Log.Warn($"WebSocket 关闭: {status} {desc}");
        if (_state == ConnectionState.Connected)
            _state = ConnectionState.Disconnected;
        await TryReconnectAsync();
    }

    private async Task OnTransportError(Exception ex)
    {
        Log.Error($"WebSocket 错误: {ex.Message}");
        if (_state == ConnectionState.Connected)
            _state = ConnectionState.Disconnected;
        await TryReconnectAsync();
    }

    private async Task TryReconnectAsync()
    {
        if (_disposed
            || _state is ConnectionState.Connecting or ConnectionState.WaitingForApproval
            || _lifetimeCts is null || _lifetimeCts.IsCancellationRequested)
            return;

        while (_reconnectAttempts < _options.MaxReconnectAttempts)
        {
            _reconnectAttempts++;
            Log.Info($"尝试重连 ({_reconnectAttempts}/{_options.MaxReconnectAttempts}) ...");

            try
            {
                await Task.Delay(_options.ReconnectDelay, _lifetimeCts.Token);
                await ConnectWithRetryAsync(_lifetimeCts.Token);
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error($"重连失败: {ex.Message}");
            }
        }

        Log.Error("已达到最大重连次数，放弃重连");
    }

    // ─── Helpers ────────────────────────────────────────────

    private ConnectParams BuildConnectParams(string nonce)
    {
        var sig = _device.Sign(
            clientId: _options.ClientId,
            clientMode: _options.ClientMode,
            role: _options.Role,
            scopes: _options.Scopes,
            token: _options.Token,
            nonce: nonce);

        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "MacIntel"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Win32"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux"
            : "unknown";

        var auth = _deviceToken is not null
            ? new AuthInfo { Token = _options.Token, DeviceToken = _deviceToken }
            : new AuthInfo { Token = _options.Token };

        return new ConnectParams
        {
            Client = new ClientInfo
            {
                Id = _options.ClientId,
                Version = _options.ClientVersion,
                Platform = platform,
                Mode = _options.ClientMode,
            },
            Role = _options.Role,
            Scopes = _options.Scopes,
            Auth = auth,
            UserAgent = $"OpenClaw-CSharp/{_options.ClientVersion} ({RuntimeInformation.OSDescription})",
            Device = new DeviceInfo
            {
                Id = _device.DeviceId,
                PublicKey = _device.PublicKeyBase64Url,
                Signature = sig.Signature,
                SignedAt = sig.SignedAt,
                Nonce = nonce,
            },
        };
    }

    private static bool IsNotPairedError(JsonElement? error)
    {
        if (error is not { } e) return false;
        var raw = e.GetRawText();
        return raw.Contains("NOT_PAIRED", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetString(JsonElement? element, string prop)
    {
        if (element is { } el && el.TryGetProperty(prop, out var val))
            return val.ToString();
        return "";
    }

    // ─── Dispose ────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _device.Dispose();
        _requests.CancelAll();
        await _lifetimeCts?.CancelAsync();
        _lifetimeCts?.Dispose();

        if (_ws is not null)
            await _ws.DisposeAsync();
    }
}

public sealed class NotPairedException : Exception
{
    public JsonElement? ErrorDetail { get; }

    public NotPairedException(string message, JsonElement? error = null) : base(message)
    {
        ErrorDetail = error;
    }
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    WaitingForApproval,
    Connected,
}