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
/// 常见用法为通过 DI 注册为 Singleton；也可为不同用户各构造一个实例并注入不同的
/// <see cref="IGatewayClientStateStore"/> / <see cref="IGatewayClientConnectionResolver"/>。
/// </summary>
public sealed partial class GatewayClient : IAsyncDisposable
{
    private readonly GatewayOptions _options;
    private readonly GatewayRequestManager _gatewayRequests;
    private readonly EventRouter _events;
    private readonly DeviceIdentity _device;
    private readonly IGatewayClientStateStore _stateStore;
    private readonly IGatewayClientConnectionResolver _connectionResolver;
    private WebSocketClient? _ws;
    private CancellationTokenSource? _lifetimeCts;
    private int _reconnectAttempts;
    private bool _disposed;
    private volatile ConnectionState _state = ConnectionState.Disconnected;

    private string? _deviceToken;
    private string[]? _cachedScopes;
    private HelloOkPayload? _helloOk;

    /// <summary>最近一次（或当前）建连调用传入的上下文，用于断线重连时把同一 state 交给 <see cref="IGatewayClientConnectionResolver"/>。</summary>
    private object? _lastConnectionState;

    /// <summary>网关事件路由器；可用 <see cref="OnEvent"/> 或直接订阅底层 API。</summary>
    public EventRouter Events => _events;

    /// <summary>底层 WebSocket 是否已打开（握手成功后通常为 true）。</summary>
    public bool IsConnected => _ws?.IsConnected ?? false;

    /// <summary>客户端连接生命周期状态（含配对等待）。</summary>
    public ConnectionState State => _state;

    /// <summary>
    /// The full hello-ok payload from the last successful connect.
    /// </summary>
    public HelloOkPayload? HelloOk => _helloOk;

    /// <summary>
    /// hello-ok 中声明的可通过 <c>type:&quot;req&quot;</c> 调用的 <c>method</c> 名称列表（含仅用于开关事件推送的指令，如 <c>sessions.subscribe</c>）。
    /// </summary>
    public IReadOnlyList<string> AvailableMethods => _helloOk?.Features?.Methods ?? [];

    /// <summary>
    /// Available event types reported by the server.
    /// </summary>
    public IReadOnlyList<string> AvailableEvents => _helloOk?.Features?.Events ?? [];

    /// <summary>
    /// 判断某 <c>req.method</c> 是否出现在 hello-ok 的 <c>features.methods</c> 中。
    /// </summary>
    /// <remarks>
    /// <c>sessions.subscribe</c> 等名称在协议里属于「事件订阅开关」请求，但仍在 <c>methods</c> 清单中登记（若网关实现）；
    /// 与 <c>features.events</c> 里列出的可推送事件名（如 <c>sessions.changed</c>）不是同一概念。
    /// </remarks>
    /// <param name="method">完整 <c>req.method</c> 字符串，与 <see cref="GatewayConstants.Methods"/> 常量一致。</param>
    /// <returns>
    /// <c>null</c>：尚未握手或方法列表为空/缺失，无法判断，调用方通常仍应发起 RPC 以兼容旧服务端；
    /// <c>true</c>：已声明；<c>false</c>：列表非空且未包含该方法。
    /// </returns>
    public bool? IsRpcMethodAdvertised(string method)
    {
        var methods = _helloOk?.Features?.Methods;
        if (methods is null || methods.Length == 0)
            return null;

        foreach (var m in methods)
        {
            if (string.Equals(m, method, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 初始化网关客户端，注入配置、请求管理器、事件路由器和设备身份。
    /// DeviceToken / scopes 不在此预加载，而是在每次建连（<see cref="ConnectAsync"/>）开始时从 <see cref="IGatewayClientStateStore"/> 拉取。
    /// </summary>
    /// <param name="options">网关连接配置（URL、认证信息、超时等）</param>
    /// <param name="gatewayRequests">请求/响应关联管理器</param>
    /// <param name="events">事件分发路由器</param>
    /// <param name="device">Ed25519 设备身份，用于握手签名</param>
    /// <param name="stateStore">DeviceToken / scopes 等状态的加载与持久化策略</param>
    /// <param name="connectionResolver">可选的 WebSocket URL 与 TLS 指纹覆盖；未指定时回退到 <paramref name="options"/> 中的默认值</param>
    public GatewayClient(
        IOptions<GatewayOptions> options,
        GatewayRequestManager gatewayRequests,
        EventRouter events,
        DeviceIdentity device,
        IGatewayClientStateStore stateStore,
        IGatewayClientConnectionResolver connectionResolver)
    {
        _options = options.Value;
        _gatewayRequests = gatewayRequests;
        _events = events;
        _device = device;
        _stateStore = stateStore;
        _connectionResolver = connectionResolver;

        Log.Info($"Device ID: {_device.DeviceId[..16]}...");
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
    public Task ConnectAsync(CancellationToken ct = default) => ConnectCoreAsync(null, ct);

    /// <summary>
    /// 建立连接并完成握手；<paramref name="state"/> 会传给 <see cref="IGatewayClientConnectionResolver"/> 与
    /// <see cref="IGatewayClientStateStore"/>，用于按用户解析 URL/指纹及读写分用户的 DeviceToken、scopes。
    /// </summary>
    /// <param name="state">任意上下文（如用户 id）；单用户场景可传 <c>null</c></param>
    /// <param name="ct">取消令牌</param>
    public Task ConnectAsync(object? state, CancellationToken ct = default) => ConnectCoreAsync(state, ct);

    /// <summary>
    /// 从 <see cref="IGatewayClientStateStore"/> 拉取与 <paramref name="state"/> 对应的 deviceToken、scopes，写入内存字段供后续 connect 签名与 auth 使用。
    /// </summary>
    private async Task LoadPersistedDeviceStateAsync(object? state, CancellationToken ct)
    {
        _deviceToken = await _stateStore.LoadDeviceTokenAsync(state, _options, ct).ConfigureAwait(false);
        _cachedScopes = await _stateStore.LoadDeviceScopesAsync(state, _options, ct).ConfigureAwait(false);
        if (_deviceToken is not null)
            Log.Debug("已加载缓存的 deviceToken");
        if (_cachedScopes is not null)
            Log.Debug($"已加载缓存的 scopes: [{string.Join(", ", _cachedScopes)}]");
    }

    private async Task ConnectCoreAsync(object? state, CancellationToken ct)
    {
        _lastConnectionState = state;
        await LoadPersistedDeviceStateAsync(state, ct).ConfigureAwait(false);
        _state = ConnectionState.Connecting;
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var challengeTcs = new TaskCompletionSource<GatewayEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        _events.On(GatewayConstants.Events.ConnectChallenge, null, (evt, _) =>
        {
            challengeTcs.TrySetResult(evt);
            return Task.CompletedTask;
        });

        await ConnectTransportAsync(state, _lifetimeCts.Token);

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

        _events.Off(GatewayConstants.Events.ConnectChallenge, null);

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

            var authErr = TryParseAuthError(connectResp.Error);

            if (authErr is { IsDeviceAuthError: true })
            {
                Log.Error($"设备认证失败: {authErr.Code} (reason={authErr.Reason})");
                throw new DeviceAuthException(errText, authErr, connectResp.Error);
            }

            if (authErr is { Code: GatewayConstants.ErrorCodes.AuthTokenMismatch, CanRetryWithDeviceToken: true } && _deviceToken is not null)
            {
                Log.Warn("AUTH_TOKEN_MISMATCH — 尝试使用缓存的 deviceToken 重试一次...");
                connectResp = await RetryConnectWithDeviceTokenOnlyAsync(state, nonce, _lifetimeCts.Token);
                if (!connectResp.Ok)
                {
                    var retryErr = connectResp.Error?.GetRawText() ?? "unknown";
                    Log.Error("deviceToken 重试仍失败，停止自动重连");
                    throw new AuthTokenMismatchException(retryErr, connectResp.Error);
                }
            }
            else if (authErr?.Code == GatewayConstants.ErrorCodes.AuthTokenMismatch)
            {
                throw new AuthTokenMismatchException(errText, connectResp.Error);
            }
            else
            {
                throw new InvalidOperationException($"connect 失败: {errText}");
            }
        }

        await ProcessHelloOkAsync(state, connectResp, _lifetimeCts.Token);

        _state = ConnectionState.Connected;
        _reconnectAttempts = 0;
    }

    /// <summary>
    /// 主动关闭当前 WebSocket 并取消生命周期令牌，使 <see cref="State"/> 回到 Disconnected；
    /// 不将客户端标记为已释放，可再次调用 <see cref="ConnectAsync"/> / <see cref="ConnectWithRetryAsync"/>。
    /// </summary>
    public Task DisconnectAsync(CancellationToken cancellationToken = default) => DisconnectAsync(null, cancellationToken);

    /// <summary>
    /// 关闭 WebSocket 并取消生命周期令牌；<paramref name="state"/> 预留与对称重载一致（当前实现未使用）。
    /// </summary>
    /// <param name="state">预留参数，保持与带 state 的 Connect 重载对称</param>
    /// <param name="cancellationToken">当前实现未观察此令牌，仅为 API 一致性保留</param>
    public async Task DisconnectAsync(object? state, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var cts = _lifetimeCts;
        _lifetimeCts = null;
        if (cts is not null)
        {
            try
            {
                await cts.CancelAsync().ConfigureAwait(false);
            }
            finally
            {
                cts.Dispose();
            }
        }

        _state = ConnectionState.Disconnected;

        var ws = _ws;
        _ws = null;
        if (ws is not null)
            await ws.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 带 NOT_PAIRED 自动重试的连接方法。
    /// 当设备未配对时，按指数退避轮询重连，直到外部审批服务完成批准。
    /// </summary>
    public Task ConnectWithRetryAsync(CancellationToken ct = default) => ConnectWithRetryAsync(null, ct);

    /// <summary>
    /// 与无 state 版本相同，但在 NOT_PAIRED 退避重试时传入 <paramref name="state"/>，以便解析 URL 与状态存储。
    /// </summary>
    /// <param name="state">与 <see cref="ConnectAsync(object?, CancellationToken)"/> 相同，用于多用户/多租户隔离</param>
    /// <param name="ct">取消令牌</param>
    public async Task ConnectWithRetryAsync(object? state, CancellationToken ct = default)
    {
        var attempt = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await ConnectCoreAsync(state, ct);
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
        if (_options.SanitizeOutboundUserMessages)
            paramsJson = GatewayOutboundMessageSanitizer.SanitizeRpcParams(method, paramsJson);

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
    private async Task<GatewayResponse> SendRequestRawAsync(string method, JsonElement parameters, CancellationToken ct)
    {
        var (id, task) = _gatewayRequests.Register(_options.RequestTimeout);
        if (_options.SanitizeOutboundUserMessages)
            parameters = GatewayOutboundMessageSanitizer.SanitizeRpcParams(method, parameters);

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
    /// 注册一个网关事件处理器（无订阅上下文，handler 的 state 恒为 <c>null</c>）。
    /// 当收到指定名称的事件时，异步调用 handler。同一事件可注册多个处理器，使用 "*" 可订阅所有事件。
    /// </summary>
    /// <param name="eventName">要监听的事件名称（如 "agent"、"chat"、"*" 等）</param>
    /// <param name="handler">异步事件处理回调</param>
    public void OnEvent(string eventName, Func<GatewayEvent, Task> handler)
        => OnEvent(eventName, null, (evt, _) => handler(evt));

    /// <summary>
    /// 注册网关事件处理器并关联 <paramref name="state"/>（如用户 id）；每次收到事件时将同一 state 传入 handler，便于多用户共享客户端实例时隔离上下文。
    /// </summary>
    /// <param name="eventName">要监听的事件名称</param>
    /// <param name="state">订阅方自定义上下文</param>
    /// <param name="handler">接收 <see cref="GatewayEvent"/> 与订阅时传入的 state</param>
    public void OnEvent(string eventName, object? state, Func<GatewayEvent, object?, Task> handler)
        => _events.On(eventName, state, handler);

    /// <summary>
    /// 便捷的聊天发送方法。向指定会话发送用户消息，触发 Agent 生成回复。
    /// 若未指定 sessionKey，自动使用服务端下发的默认会话键。
    /// </summary>
    /// <param name="userMessage">用户消息文本</param>
    /// <param name="sessionKey">目标会话键，为 null 时自动选择默认会话</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示消息是否成功入队处理</returns>
    public Task<GatewayResponse> ChatAsync(string userMessage, string? sessionKey = null,
        CancellationToken ct = default)
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
    /// <param name="state"></param>
    /// <param name="resp">connect 请求的网关响应</param>
    private async Task ProcessHelloOkAsync(object? state, GatewayResponse resp, CancellationToken ct)
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
                await _stateStore.SaveDeviceTokenAsync(dt, state, _options, ct).ConfigureAwait(false);
                Log.Success("  DeviceToken 已保存");
            }

            if (auth.Scopes is { Length: > 0 } scopes)
            {
                _cachedScopes = scopes;
                await _stateStore.SaveDeviceScopesAsync(scopes, state, _options, ct).ConfigureAwait(false);
                Log.Debug("  Scopes 已缓存");
            }

            await PersistBootstrapHandoffTokensAsync(state, auth.DeviceTokens, ct);
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

    // ─── Bootstrap Handoff Tokens ─────────────────────────

    /// <summary>
    /// 处理 hello-ok.auth.deviceTokens（bootstrap handoff tokens）。
    /// 仅在本次连接使用了 bootstrap auth 且传输层可信（wss:// 或 loopback）时才持久化。
    /// </summary>
    private async Task PersistBootstrapHandoffTokensAsync(object? state, DeviceTokenEntry[]? tokens, CancellationToken ct)
    {
        if (tokens is not { Length: > 0 }) return;

        if (!await IsTransportTrustedAsync(state, ct))
        {
            Log.Debug($"  收到 {tokens.Length} 个 bootstrap handoff token，但传输层不可信，跳过持久化");
            return;
        }

        foreach (var entry in tokens)
        {
            Log.Debug(
                $"  Bootstrap handoff token: role={entry.Role}, scopes=[{string.Join(", ", entry.Scopes ?? [])}]");
        }

        var primary = tokens[0];
        _deviceToken = primary.DeviceToken;
        await _stateStore.SaveDeviceTokenAsync(primary.DeviceToken, state, _options, ct).ConfigureAwait(false);
        if (primary.Scopes is { Length: > 0 } scopes)
        {
            _cachedScopes = scopes;
            await _stateStore.SaveDeviceScopesAsync(scopes, state, _options, ct).ConfigureAwait(false);
        }

        Log.Success($"  已持久化 bootstrap handoff token（共 {tokens.Length} 个）");
    }

    /// <summary>
    /// 判断当前 WebSocket 传输是否可信（wss:// 或 loopback 地址）。
    /// </summary>
    private async ValueTask<bool> IsTransportTrustedAsync(object? state, CancellationToken ct)
    {
        var url = await GetEffectiveWebSocketUrlAsync(state, ct);
        if (url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            return true;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var host = uri.Host;
            return host is "localhost" or "127.0.0.1" or "::1"
                   || host.StartsWith("[::1]", StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>
    /// 解析实际建连使用的 WebSocket URL（覆盖为空时使用 <see cref="GatewayOptions.Url"/>）。
    /// </summary>
    internal async ValueTask<string> GetEffectiveWebSocketUrlAsync(object? state, CancellationToken ct)
    {
        var o = await _connectionResolver.ResolveWebSocketUrlAsync(state, _options, ct);
        return string.IsNullOrWhiteSpace(o) ? _options.Url : o;
    }

    /// <summary>
    /// 解析 TLS 指纹（覆盖为空时使用 <see cref="GatewayOptions.TlsFingerprint"/>）。
    /// </summary>
    internal async ValueTask<string?> GetEffectiveTlsFingerprintAsync(object? state, CancellationToken ct)
    {
        var o = await _connectionResolver.ResolveTlsFingerprintAsync(state, _options, ct);
        return string.IsNullOrWhiteSpace(o) ? _options.TlsFingerprint : o;
    }

    // ─── Transport ─────────────────────────────────────────

    /// <summary>
    /// 建立底层 WebSocket 传输连接。
    /// 若已有旧连接则先释放，然后创建新的 <see cref="WebSocketClient"/> 并绑定消息、关闭、错误回调。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    private async Task ConnectTransportAsync(object? state, CancellationToken ct)
    {
        if (_ws is not null)
            await _ws.DisposeAsync();

        var url = await GetEffectiveWebSocketUrlAsync(state, ct);
        var tlsFp = await GetEffectiveTlsFingerprintAsync(state, ct);
        _ws = new WebSocketClient(new Uri(url), tlsFp);
        _ws.OnMessage += OnRawMessage;
        _ws.OnClosed += OnTransportClosed;
        _ws.OnError += OnTransportError;

        Log.Info($"连接 {url} ...");
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
                await ConnectWithRetryAsync(_lastConnectionState, _lifetimeCts.Token);
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
    /// 构建 connect 请求的参数对象。
    /// Auth 优先级：explicit shared token/password → explicit deviceToken → stored per-device token → bootstrap token。
    /// Scope 策略：显式 deviceToken 或 scopes 配置时使用调用方请求的 scope 集合；
    /// 仅在复用 stored per-device token 时，合并缓存的 approved scope set 以保留已授予的权限。
    /// </summary>
    /// <param name="nonce">服务端下发的随机 nonce</param>
    /// <param name="deviceTokenOnly">为 true 时仅发送 deviceToken（AUTH_TOKEN_MISMATCH 重试场景）</param>
    /// <returns>完整的 connect 请求参数</returns>
    private ConnectParams BuildConnectParams(string nonce, bool deviceTokenOnly = false)
    {
        var effectiveScopes = ResolveEffectiveScopes();

        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? GatewayConstants.Platforms.MacIntel
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GatewayConstants.Platforms.Win32
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? GatewayConstants.Platforms.Linux
            : GatewayConstants.Platforms.Unknown;

        var deviceFamily = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "desktop"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "desktop"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "desktop"
            : "unknown";

        var sig = _device.Sign(
            clientId: _options.ClientId,
            clientMode: _options.ClientMode,
            role: _options.Role,
            scopes: effectiveScopes,
            token: _options.Token,
            nonce: nonce,
            platform: platform,
            deviceFamily: deviceFamily);

        var auth = BuildAuthInfo(deviceTokenOnly);

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
            Scopes = effectiveScopes,
            Auth = auth,
            UserAgent = string.Format(GatewayConstants.Transport.UserAgentTemplate, _options.ClientVersion,
                RuntimeInformation.OSDescription),
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
    /// 构建 auth 信息。
    /// <paramref name="deviceTokenOnly"/> 为 true 时仅发送 deviceToken，不发送 token/password
    /// （用于 AUTH_TOKEN_MISMATCH bounded retry 场景）。
    /// </summary>
    private AuthInfo BuildAuthInfo(bool deviceTokenOnly)
    {
        if (deviceTokenOnly && _deviceToken is not null)
            return new AuthInfo { DeviceToken = _deviceToken };

        return new AuthInfo
        {
            Token = !string.IsNullOrEmpty(_options.Token) ? _options.Token : null,
            Password = !string.IsNullOrEmpty(_options.Password) ? _options.Password : null,
            DeviceToken = _deviceToken,
        };
    }

    /// <summary>
    /// 根据协议优先级解析有效的 scope 集合：
    /// - 用户显式配置了 scopes → 使用显式值（调用方权威）
    /// - 复用 stored per-device token 且有缓存 scopes → 使用缓存值以保留已授予的权限
    /// - 以上都不满足 → 使用配置默认值
    /// </summary>
    private string[] ResolveEffectiveScopes()
    {
        if (_cachedScopes is { Length: > 0 } && _deviceToken is not null)
            return _cachedScopes;
        return _options.Scopes;
    }

    /// <summary>
    /// AUTH_TOKEN_MISMATCH bounded retry：断开当前传输，重新建连并仅用 deviceToken 做一次重试。
    /// </summary>
    private async Task<GatewayResponse> RetryConnectWithDeviceTokenOnlyAsync(object? state, string previousNonce, CancellationToken ct)
    {
        if (_ws is not null) await _ws.DisposeAsync();

        var challengeTcs = new TaskCompletionSource<GatewayEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        _events.On(GatewayConstants.Events.ConnectChallenge, null, (evt, _) =>
        {
            challengeTcs.TrySetResult(evt);
            return Task.CompletedTask;
        });

        await ConnectTransportAsync(state, ct);
        var challengeEvt = await challengeTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        _events.Off(GatewayConstants.Events.ConnectChallenge, null);

        var nonce = "";
        if (challengeEvt.Payload.HasValue)
            nonce = GetString(challengeEvt.Payload.Value, "nonce");

        var connectParams = BuildConnectParams(nonce, deviceTokenOnly: true);
        return await SendRequestAsync(GatewayConstants.Methods.Connect, connectParams, ct);
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
    /// 尝试从 connect 错误响应中解析结构化的认证错误详情。
    /// 查找路径为 <c>error.details</c> 或 <c>error</c> 本身中的 <c>code</c> 字段。
    /// </summary>
    private static AuthErrorDetails? TryParseAuthError(JsonElement? error)
    {
        if (error is not { } e) return null;

        try
        {
            if (e.TryGetProperty("details", out var details))
                return JsonSerializer.Deserialize<AuthErrorDetails>(details.GetRawText(),
                    JsonDefaults.SerializerOptions);

            if (e.TryGetProperty("code", out _))
                return JsonSerializer.Deserialize<AuthErrorDetails>(e.GetRawText(), JsonDefaults.SerializerOptions);
        }
        catch (JsonException)
        {
        }

        var raw = e.GetRawText();
        if (raw.Contains(GatewayConstants.ErrorCodes.AuthTokenMismatch, StringComparison.OrdinalIgnoreCase))
            return new AuthErrorDetails { Code = GatewayConstants.ErrorCodes.AuthTokenMismatch };

        return null;
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

    // ─── 单元测试专用钩子（InternalsVisibleTo）────────────────

    /// <summary>
    /// 仅用于单元测试：将入站 JSON 走与真实 WebSocket 相同的消息管线（响应帧 / 事件帧分发）。
    /// </summary>
    /// <param name="json">模拟 WebSocket 收到的原始 JSON 文本</param>
    internal Task SimulateIncomingJsonForTests(string json) => OnRawMessage(json);

    /// <summary>
    /// 仅用于单元测试：挂载可替换的 <see cref="WebSocketClient"/>（通常为子类，用于捕获出站请求并回写响应）。
    /// </summary>
    /// <param name="webSocket">模拟或桩实现的 WebSocket 客户端</param>
    internal void AttachWebSocketForTests(WebSocketClient webSocket) => _ws = webSocket;

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
/// 认证令牌不匹配异常。当 connect 请求返回 AUTH_TOKEN_MISMATCH 错误时抛出，
/// 表示共享密钥/密码与网关当前配置不一致。
/// 客户端应停止自动重连并引导操作员检查认证配置。
/// </summary>
public sealed class AuthTokenMismatchException : Exception
{
    /// <summary>服务端返回的错误详情 JSON</summary>
    public JsonElement? ErrorDetail { get; }

    /// <summary>使用错误说明与可选的原始 error JSON 构造异常。</summary>
    /// <param name="message">人类可读说明</param>
    /// <param name="error">服务端 <c>error</c> 对象，可为 null</param>
    public AuthTokenMismatchException(string message, JsonElement? error = null) : base(message)
    {
        ErrorDetail = error;
    }
}

/// <summary>
/// 设备认证迁移诊断异常。当 connect 请求返回 <c>DEVICE_AUTH_*</c> 错误码时抛出，
/// 表示旧版客户端的设备签名流程与网关要求的 v2/v3 payload 不兼容。
/// <para>
/// 迁移指引：始终等待 <c>connect.challenge</c> → 使用服务端 nonce 签名 v2 payload →
/// 将同一 nonce 放入 <c>connect.params.device.nonce</c>。
/// </para>
/// </summary>
public sealed class DeviceAuthException : Exception
{
    /// <summary>服务端返回的错误详情 JSON</summary>
    public JsonElement? ErrorDetail { get; }

    /// <summary>结构化的 <c>error.details</c>，含 <c>code</c> 和 <c>reason</c></summary>
    public AuthErrorDetails Details { get; }

    /// <summary>设备认证错误码（<c>DEVICE_AUTH_*</c>），等同于 <see cref="Details"/>.<see cref="AuthErrorDetails.Code"/></summary>
    public string? DeviceAuthCode => Details.Code;

    /// <summary>诊断原因标识符，等同于 <see cref="Details"/>.<see cref="AuthErrorDetails.Reason"/></summary>
    public string? Reason => Details.Reason;

    /// <summary>使用说明、结构化详情与可选原始 JSON 构造异常。</summary>
    /// <param name="message">人类可读说明</param>
    /// <param name="details">从 <c>error.details</c> 解析的字段</param>
    /// <param name="error">完整 error 元素，可为 null</param>
    public DeviceAuthException(string message, AuthErrorDetails details, JsonElement? error = null) : base(message)
    {
        Details = details;
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