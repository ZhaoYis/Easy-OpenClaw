using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 后台健康监控服务：结合主动 RPC 轮询和被动事件监听，持续追踪网关服务状态。
/// <para>主动轮询：每隔 <see cref="GatewayOptions.HealthPollIntervalSeconds"/> 秒调用 health RPC。</para>
/// <para>被动监听：订阅 tick / health / heartbeat 事件并检测超时。</para>
/// 状态变化时通过 <see cref="HealthStateChanged"/> 事件通知应用层。
/// </summary>
public sealed class HealthMonitorService : BackgroundService
{
    private readonly GatewayClient _client;
    private readonly GatewayEventSubscriber _subscriber;
    private readonly GatewayOptions _options;

    private DateTime? _lastTickReceived;
    private DateTime? _lastHeartbeatReceived;
    private DateTime? _lastHealthCheck;
    private bool? _lastHealthOk;
    private bool _previousIsHealthy = true;

    /// <summary>当前健康状态快照</summary>
    public GatewayHealthState CurrentState { get; private set; } = new() { IsHealthy = true, IsConnected = false };

    /// <summary>健康状态发生变更（healthy ↔ unhealthy）时触发</summary>
    public event Action<GatewayHealthState>? HealthStateChanged;

    /// <summary>
    /// 注入 <see cref="GatewayClient"/>、已注册的 <see cref="GatewayEventSubscriber"/> 与选项。
    /// 需在 Host 启动前完成 <see cref="GatewayEventSubscriber.RegisterAll"/> 等订阅，以便收到 tick/health/heartbeat。
    /// </summary>
    public HealthMonitorService(
        GatewayClient client,
        GatewayEventSubscriber subscriber,
        IOptions<GatewayOptions> options)
    {
        _client = client;
        _subscriber = subscriber;
        _options = options.Value;
    }

    /// <summary>
    /// 订阅 <see cref="GatewayEventSubscriber"/> 的被动信号后再调用基类启动。
    /// </summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber.TickReceived += OnTickReceived;
        _subscriber.HealthReceived += OnHealthReceived;
        _subscriber.HeartbeatReceived += OnHeartbeatReceived;

        Log.Trace("INFO", $"[HealthMonitor] 健康监控已启动 (轮询间隔={_options.HealthPollIntervalSeconds}s, tick超时={_options.TickTimeoutSeconds}s, heartbeat超时={_options.HeartbeatTimeoutSeconds}s)");
        return base.StartAsync(cancellationToken);
    }

    /// <summary>取消订阅被动事件后停止。</summary>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _subscriber.TickReceived -= OnTickReceived;
        _subscriber.HealthReceived -= OnHealthReceived;
        _subscriber.HeartbeatReceived -= OnHeartbeatReceived;

        Log.Trace("INFO", "[HealthMonitor] 健康监控已停止");
        return base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// 若启用 <see cref="GatewayOptions.EnableHealthMonitor"/>，在客户端已连接后按间隔调用 health RPC 并结合 tick/heartbeat 超时更新 <see cref="CurrentState"/>。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableHealthMonitor)
        {
            Log.Trace("INFO", "[HealthMonitor] EnableHealthMonitor=false，后台轮询未启用");
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(_options.HealthPollIntervalSeconds);

        // 等待客户端完成连接后再开始轮询，避免启动阶段误报"未连接"
        while (!stoppingToken.IsCancellationRequested && !_client.IsConnected)
        {
            try { await Task.Delay(500, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }

        Log.Trace("INFO", "[HealthMonitor] 客户端已连接，开始持续健康探测");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_client.IsConnected)
            {
                UpdateState(BuildState(unhealthyReason: "WebSocket 未连接"));
            }
            else
            {
                await PollHealthAsync(stoppingToken);
                EvaluatePassiveSignals();
            }

            try { await Task.Delay(pollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>调用 <c>health</c> RPC 并更新最近一次探测时间与结果字段。</summary>
    private async Task PollHealthAsync(CancellationToken ct)
    {
        try
        {
            var resp = await _client.SendRequestAsync(
                GatewayConstants.Methods.Health, new { }, ct);
            _lastHealthCheck = DateTime.UtcNow;

            if (resp.Ok)
            {
                _lastHealthOk = true;

                if (resp.Payload is { } p && p.TryGetProperty("ok", out var okEl))
                    _lastHealthOk = okEl.GetBoolean();

                Log.Trace("POLL",
                    $"[HealthMonitor] health={(_lastHealthOk == true ? "ok" : "fail")}, " +
                    $"tick={FormatElapsed(_lastTickReceived)}, hb={FormatElapsed(_lastHeartbeatReceived)}");
                UpdateState(BuildState());
            }
            else
            {
                _lastHealthOk = false;
                var err = resp.Error?.GetRawText() ?? "unknown";
                UpdateState(BuildState(unhealthyReason: $"health RPC 失败: {err}"));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _lastHealthOk = false;
            UpdateState(BuildState(unhealthyReason: $"health RPC 异常: {ex.Message}"));
        }
    }

    /// <summary>将上次事件时间格式化为 “Xs ago” 或 “n/a”。</summary>
    private static string FormatElapsed(DateTime? ts)
    {
        if (ts is null) return "n/a";
        var elapsed = DateTime.UtcNow - ts.Value;
        return $"{elapsed.TotalSeconds:F0}s ago";
    }

    /// <summary>根据 <see cref="GatewayOptions.TickTimeoutSeconds"/> 与 <see cref="GatewayOptions.HeartbeatTimeoutSeconds"/> 判定被动信号是否超时。</summary>
    private void EvaluatePassiveSignals()
    {
        var now = DateTime.UtcNow;

        if (_lastTickReceived.HasValue)
        {
            var elapsed = now - _lastTickReceived.Value;
            if (elapsed.TotalSeconds > _options.TickTimeoutSeconds)
            {
                UpdateState(BuildState(unhealthyReason: $"tick 超时 ({elapsed.TotalSeconds:F0}s > {_options.TickTimeoutSeconds}s)"));
                return;
            }
        }

        if (_lastHeartbeatReceived.HasValue)
        {
            var elapsed = now - _lastHeartbeatReceived.Value;
            if (elapsed.TotalSeconds > _options.HeartbeatTimeoutSeconds)
            {
                UpdateState(BuildState(unhealthyReason: $"heartbeat 超时 ({elapsed.TotalSeconds:F0}s > {_options.HeartbeatTimeoutSeconds}s)"));
                return;
            }
        }

        UpdateState(BuildState());
    }

    /// <summary>根据连接、RPC 结果与被动超时原因组装当前健康快照。</summary>
    private GatewayHealthState BuildState(string? unhealthyReason = null)
    {
        var isConnected = _client.IsConnected;
        var isHealthy = isConnected
                        && unhealthyReason is null
                        && _lastHealthOk != false;

        return new GatewayHealthState
        {
            IsHealthy = isHealthy,
            LastHealthCheck = _lastHealthCheck,
            LastHealthOk = _lastHealthOk,
            LastTickReceived = _lastTickReceived,
            LastHeartbeatReceived = _lastHeartbeatReceived,
            UnhealthyReason = isHealthy ? null : (unhealthyReason ?? "网关报告不健康"),
            IsConnected = isConnected,
        };
    }

    /// <summary>写入 <see cref="CurrentState"/>；若 healthy 状态翻转则触发 <see cref="HealthStateChanged"/>。</summary>
    private void UpdateState(GatewayHealthState newState)
    {
        CurrentState = newState;

        if (newState.IsHealthy == _previousIsHealthy)
            return;

        _previousIsHealthy = newState.IsHealthy;

        if (newState.IsHealthy)
            Log.Trace(" OK ", "[HealthMonitor] 服务恢复健康");
        else
            Log.Trace("WARN", $"[HealthMonitor] 服务不健康: {newState.UnhealthyReason}");

        HealthStateChanged?.Invoke(newState);
    }

    // ─── Passive Event Handlers ──────────────────────────

    /// <summary><see cref="GatewayEventSubscriber.TickReceived"/> 回调：刷新 tick 时间戳。</summary>
    private void OnTickReceived()
    {
        _lastTickReceived = DateTime.UtcNow;
    }

    /// <summary><see cref="GatewayEventSubscriber.HealthReceived"/> 回调：记录网关推送的健康结论。</summary>
    private void OnHealthReceived(HealthNotification notification)
    {
        _lastHealthOk = notification.Ok;
        _lastHealthCheck = DateTime.UtcNow;
    }

    /// <summary><see cref="GatewayEventSubscriber.HeartbeatReceived"/> 回调：刷新心跳时间戳。</summary>
    private void OnHeartbeatReceived(HeartbeatNotification notification)
    {
        _lastHeartbeatReceived = DateTime.UtcNow;
    }
}
