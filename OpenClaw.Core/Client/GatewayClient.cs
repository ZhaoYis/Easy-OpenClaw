using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;
using OpenClaw.Core.Transport;

namespace OpenClaw.Core.Client;

/// <summary>
/// High-level client that wraps WebSocket transport, request/response correlation,
/// event routing, handshake (with Ed25519 device auth), reconnection, and chat helpers.
/// Singleton 生命周期：维护 WebSocket 连接状态，整个应用中只需一个实例。
/// </summary>
public sealed partial class GatewayClient : IAsyncDisposable
{
    private readonly GatewayOptions _options;
    private readonly GatewayRequestManager _gatewayRequests;
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

    /// <summary>
    /// 初始化网关客户端，注入配置、请求管理器、事件路由器和设备身份。
    /// 构造时尝试从磁盘加载缓存的 DeviceToken 以支持免审批重连。
    /// </summary>
    /// <param name="options">网关连接配置（URL、认证信息、超时等）</param>
    /// <param name="gatewayRequests">请求/响应关联管理器</param>
    /// <param name="events">事件分发路由器</param>
    /// <param name="device">Ed25519 设备身份，用于握手签名</param>
    public GatewayClient(
        IOptions<GatewayOptions> options,
        GatewayRequestManager gatewayRequests,
        EventRouter events,
        DeviceIdentity device)
    {
        _options = options.Value;
        _gatewayRequests = gatewayRequests;
        _events = events;
        _device = device;

        _deviceToken = LoadDeviceToken();

        Log.Info($"Device ID: {_device.DeviceId[..16]}...");
        if (_deviceToken is not null)
            Log.Debug("已加载缓存的 deviceToken");
    }

    // ─── Public API ────────────────────────────────────────

    /// <summary>
    /// 建立到网关的 WebSocket 连接并完成完整握手流程：
    /// 1. 建立 WebSocket 传输层连接
    /// 2. 等待服务端下发 connect_challenge 事件（含 nonce）
    /// 3. 使用 Ed25519 签名 nonce 并发送 connect 请求
    /// 4. 处理 hello-ok 响应，提取服务端能力和认证信息
    /// 若设备未配对（NOT_PAIRED），抛出 <see cref="NotPairedException"/>。
    /// </summary>
    /// <param name="ct">取消令牌，可用于中断连接流程</param>
    /// <exception cref="NotPairedException">设备未配对时抛出</exception>
    /// <exception cref="InvalidOperationException">连接因其他原因失败时抛出</exception>
    /// <exception cref="TimeoutException">等待 challenge 超过 10 秒时抛出</exception>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _state = ConnectionState.Connecting;
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var challengeTcs = new TaskCompletionSource<GatewayEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        _events.On(GatewayConstants.Events.ConnectChallenge, evt =>
        {
            challengeTcs.TrySetResult(evt);
            return Task.CompletedTask;
        });

        await ConnectTransportAsync(_lifetimeCts.Token);

        Log.Info($"等待 {GatewayConstants.Events.ConnectChallenge} ...");

        var challengeEvt = await challengeTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), _lifetimeCts.Token);
        Log.Success("收到 challenge");

        var nonce = "";
        if (challengeEvt.Payload.HasValue)
        {
            nonce = GetString(challengeEvt.Payload.Value, "nonce");
            var ts = GetString(challengeEvt.Payload.Value, "ts");
            Log.Debug($"  nonce={nonce}, ts={ts}");
        }

        _events.Off(GatewayConstants.Events.ConnectChallenge);

        var connectParams = BuildConnectParams(nonce);
        var connectResp = await SendRequestAsync(GatewayConstants.Methods.Connect, connectParams, _lifetimeCts.Token);

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

    /// <summary>
    /// 基于重试次数计算指数退避延迟时间。
    /// 公式：delay = min(baseDelay * 1.5^(attempt-1), maxDelay)。
    /// </summary>
    /// <param name="attempt">当前重试次数（从 1 开始）</param>
    /// <returns>计算出的延迟时间</returns>
    private TimeSpan CalculateBackoff(int attempt)
    {
        var baseMs = _options.PairingRetryDelay.TotalMilliseconds;
        var maxMs = _options.PairingRetryMaxDelay.TotalMilliseconds;
        var delayMs = Math.Min(baseMs * Math.Pow(1.5, attempt - 1), maxMs);
        return TimeSpan.FromMilliseconds(delayMs);
    }

    /// <summary>
    /// 发送带有强类型参数的 RPC 请求到网关。
    /// 自动生成唯一请求 ID，将参数序列化为 JSON，通过 WebSocket 发送，
    /// 并返回一个 Task 等待对应 ID 的响应到达。
    /// </summary>
    /// <typeparam name="T">请求参数类型，将被 JSON 序列化</typeparam>
    /// <param name="method">RPC 方法名（如 "chat.send"）</param>
    /// <param name="parameters">请求参数对象</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，包含成功/失败状态和载荷数据</returns>
    public async Task<GatewayResponse> SendRequestAsync<T>(string method, T parameters, CancellationToken ct = default)
    {
        var (id, task) = _gatewayRequests.Register(_options.RequestTimeout);
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

    /// <summary>
    /// 发送带有原始 <see cref="JsonElement"/> 参数的 RPC 请求到网关。
    /// 适用于参数已经是 JSON 结构的场景，避免二次序列化。
    /// </summary>
    /// <param name="method">RPC 方法名</param>
    /// <param name="parameters">已序列化的 JSON 参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应</returns>
    public Task<GatewayResponse> SendRequestAsync(string method, JsonElement parameters, CancellationToken ct = default)
    {
        return SendRequestRawAsync(method, parameters, ct);
    }

    /// <summary>
    /// 内部原始请求发送方法。注册请求到 <see cref="GatewayRequestManager"/>，
    /// 构建 <see cref="GatewayRequest"/> 帧并通过 WebSocket 发送，返回响应 Task。
    /// </summary>
    /// <param name="method">RPC 方法名</param>
    /// <param name="parameters">JSON 格式的请求参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应</returns>
    internal async Task<GatewayResponse> SendRequestRawAsync(string method, JsonElement parameters, CancellationToken ct)
    {
        var (id, task) = _gatewayRequests.Register(_options.RequestTimeout);

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

    /// <summary>
    /// 注册一个网关事件处理器。当收到指定名称的事件时，异步调用 handler。
    /// 同一事件可注册多个处理器，使用 "*" 可订阅所有事件。
    /// </summary>
    /// <param name="eventName">要监听的事件名称（如 "agent"、"chat"、"*" 等）</param>
    /// <param name="handler">异步事件处理回调</param>
    public void OnEvent(string eventName, Func<GatewayEvent, Task> handler)
        => _events.On(eventName, handler);

    /// <summary>
    /// 便捷的聊天发送方法。向指定会话发送用户消息，触发 Agent 生成回复。
    /// 若未指定 sessionKey，自动使用服务端下发的默认会话键。
    /// </summary>
    /// <param name="userMessage">用户消息文本</param>
    /// <param name="sessionKey">目标会话键，为 null 时自动选择默认会话</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示消息是否成功入队处理</returns>
    public Task<GatewayResponse> ChatAsync(string userMessage, string? sessionKey = null, CancellationToken ct = default)
    {
        var key = sessionKey
                  ?? _helloOk?.Snapshot?.SessionDefaults?.MainSessionKey
                  ?? GatewayConstants.DefaultSessionKey;

        var param = new ChatSendParams
        {
            SessionKey = key,
            Message = userMessage,
        };
        return SendRequestAsync(GatewayConstants.Methods.ChatSend, param, ct);
    }

    // ─── hello-ok Processing ───────────────────────────────

    /// <summary>
    /// 解析 connect 请求返回的 hello-ok 载荷，提取服务端信息并缓存。
    /// 处理内容包括：服务端版本/连接 ID、支持的方法和事件列表、策略参数、
    /// 认证结果（角色/权限/DeviceToken）以及系统快照（运行状态、会话默认值等）。
    /// 若响应中包含新的 DeviceToken，自动持久化到磁盘供后续免审批重连使用。
    /// </summary>
    /// <param name="resp">connect 请求的网关响应</param>
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

    /// <summary>
    /// 将 hello-ok 中的系统快照关键信息输出到日志：
    /// 网关运行时间、认证模式、可用更新、默认 Agent/会话、在线设备数、
    /// 健康状态（子系统通道、Agent 数量、会话数量）以及配置路径等。
    /// </summary>
    /// <param name="snapshot">系统快照信息</param>
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

    /// <summary>
    /// 从磁盘文件加载缓存的 DeviceToken。
    /// DeviceToken 是服务端在首次配对成功后下发的令牌，持有后可跳过审批直接重连。
    /// </summary>
    /// <returns>缓存的 DeviceToken 字符串，文件不存在或为空时返回 null</returns>
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

    /// <summary>
    /// 将 DeviceToken 持久化到磁盘文件，供后续应用重启时免审批重连使用。
    /// 自动创建父目录（若不存在）。写入失败时仅记录警告，不抛出异常。
    /// </summary>
    /// <param name="token">要保存的 DeviceToken 字符串</param>
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

    /// <summary>
    /// 建立底层 WebSocket 传输连接。
    /// 若已有旧连接则先释放，然后创建新的 <see cref="WebSocketClient"/> 并绑定消息、关闭、错误回调。
    /// </summary>
    /// <param name="ct">取消令牌</param>
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

    /// <summary>
    /// WebSocket 原始消息处理入口。将 JSON 字符串反序列化为 <see cref="RawFrame"/>，
    /// 根据帧类型分发到 <see cref="HandleResponse"/> 或 <see cref="HandleEvent"/>。
    /// JSON 解析失败时记录错误日志但不抛出异常，确保连接不会因单条消息错误而断开。
    /// </summary>
    /// <param name="json">WebSocket 接收到的原始 JSON 字符串</param>
    private async Task OnRawMessage(string json)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<RawFrame>(json, JsonDefaults.SerializerOptions);
            if (raw is null) return;

            switch (raw.Type)
            {
                case GatewayConstants.FrameTypes.Response:
                    HandleResponse(raw);
                    break;

                case GatewayConstants.FrameTypes.Event:
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

    /// <summary>
    /// 处理 "response" 类型的帧。将原始帧转换为 <see cref="GatewayResponse"/>，
    /// 通过 <see cref="GatewayRequestManager.TryComplete"/> 与对应的请求进行关联。
    /// 若找不到匹配的待处理请求（可能已超时），记录警告日志。
    /// </summary>
    /// <param name="raw">原始响应帧</param>
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

        if (!_gatewayRequests.TryComplete(resp.Id, resp))
        {
            Log.Warn($"收到未匹配的响应 id={resp.Id}");
        }
    }

    /// <summary>
    /// 处理 "event" 类型的帧。将原始帧转换为 <see cref="GatewayEvent"/>，
    /// 通过 <see cref="EventRouter.DispatchAsync"/> 分发到所有已注册的事件处理器。
    /// </summary>
    /// <param name="raw">原始事件帧</param>
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

    /// <summary>
    /// WebSocket 连接关闭回调。记录关闭原因并触发自动重连逻辑。
    /// </summary>
    /// <param name="status">WebSocket 关闭状态码</param>
    /// <param name="desc">关闭描述信息</param>
    private async Task OnTransportClosed(System.Net.WebSockets.WebSocketCloseStatus? status, string? desc)
    {
        Log.Warn($"WebSocket 关闭: {status} {desc}");
        if (_state == ConnectionState.Connected)
            _state = ConnectionState.Disconnected;
        await TryReconnectAsync();
    }

    /// <summary>
    /// WebSocket 传输层错误回调。记录异常信息并触发自动重连逻辑。
    /// </summary>
    /// <param name="ex">传输层异常</param>
    private async Task OnTransportError(Exception ex)
    {
        Log.Error($"WebSocket 错误: {ex.Message}");
        if (_state == ConnectionState.Connected)
            _state = ConnectionState.Disconnected;
        await TryReconnectAsync();
    }

    /// <summary>
    /// 自动重连逻辑。在连接异常断开后，按配置的重连间隔和最大重试次数尝试恢复连接。
    /// 跳过以下场景：客户端已释放、正在连接中、等待配对审批、或已取消。
    /// 每次重连调用 <see cref="ConnectWithRetryAsync"/> 以支持 NOT_PAIRED 场景。
    /// </summary>
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

    /// <summary>
    /// 构建 connect 请求的参数对象。包含：
    /// - 客户端身份信息（ID、版本、平台、模式）
    /// - 角色和权限范围
    /// - 认证令牌（包含缓存的 DeviceToken，若存在）
    /// - User-Agent 字符串
    /// - Ed25519 设备签名（对 nonce 签名以证明设备身份）
    /// </summary>
    /// <param name="nonce">服务端下发的随机 nonce</param>
    /// <returns>完整的 connect 请求参数</returns>
    private ConnectParams BuildConnectParams(string nonce)
    {
        var sig = _device.Sign(
            clientId: _options.ClientId,
            clientMode: _options.ClientMode,
            role: _options.Role,
            scopes: _options.Scopes,
            token: _options.Token,
            nonce: nonce);

        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? GatewayConstants.Platforms.MacIntel
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GatewayConstants.Platforms.Win32
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? GatewayConstants.Platforms.Linux
            : GatewayConstants.Platforms.Unknown;

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
            UserAgent = string.Format(GatewayConstants.Transport.UserAgentTemplate, _options.ClientVersion, RuntimeInformation.OSDescription),
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

    /// <summary>
    /// 检测 connect 错误响应是否为 NOT_PAIRED 错误（设备未配对）。
    /// 通过在错误 JSON 文本中搜索 NOT_PAIRED 标识符来判断。
    /// </summary>
    /// <param name="error">响应中的错误 JSON 元素</param>
    /// <returns>是否为 NOT_PAIRED 错误</returns>
    private static bool IsNotPairedError(JsonElement? error)
    {
        if (error is not { } e) return false;
        var raw = e.GetRawText();
        return raw.Contains(GatewayConstants.ErrorCodes.NotPaired, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 从可空的 <see cref="JsonElement"/> 中安全提取指定属性的字符串值。
    /// </summary>
    /// <param name="element">可空的 JSON 元素</param>
    /// <param name="prop">属性名</param>
    /// <returns>属性值字符串，不存在时返回空字符串</returns>
    private static string GetString(JsonElement? element, string prop)
    {
        if (element is { } el && el.TryGetProperty(prop, out var val))
            return val.ToString();
        return "";
    }

    // ─── Dispose ────────────────────────────────────────────

    /// <summary>
    /// 异步释放网关客户端的所有资源：
    /// 释放设备密钥、取消所有进行中的请求、取消生命周期令牌、关闭 WebSocket 连接。
    /// 幂等操作，多次调用安全。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _device.Dispose();
        _gatewayRequests.CancelAll();
        if (_lifetimeCts is not null)
            await _lifetimeCts.CancelAsync();
        _lifetimeCts?.Dispose();

        if (_ws is not null)
            await _ws.DisposeAsync();
    }
}

/// <summary>
/// 设备未配对异常。当 connect 请求返回 NOT_PAIRED 错误时抛出，
/// 表示当前设备尚未获得网关的配对审批，需要等待管理员批准。
/// </summary>
public sealed class NotPairedException : Exception
{
    /// <summary>服务端返回的错误详情 JSON</summary>
    public JsonElement? ErrorDetail { get; }

    /// <summary>
    /// 创建未配对异常实例。
    /// </summary>
    /// <param name="message">错误描述信息</param>
    /// <param name="error">可选的服务端错误 JSON 详情</param>
    public NotPairedException(string message, JsonElement? error = null) : base(message)
    {
        ErrorDetail = error;
    }
}

/// <summary>
/// 网关连接状态枚举，描述客户端与网关之间的连接生命周期。
/// </summary>
public enum ConnectionState
{
    /// <summary>已断开连接（初始状态或连接丢失后的状态）</summary>
    Disconnected,

    /// <summary>正在建立连接和握手</summary>
    Connecting,

    /// <summary>设备未配对，等待管理员审批</summary>
    WaitingForApproval,

    /// <summary>已连接且握手完成，可正常收发消息</summary>
    Connected,
}