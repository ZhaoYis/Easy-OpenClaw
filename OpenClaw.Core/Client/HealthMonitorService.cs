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

    public HealthMonitorService(
        GatewayClient client,
        GatewayEventSubscriber subscriber,
        IOptions<GatewayOptions> options)
    {
        _client = client;
        _subscriber = subscriber;
        _options = options.Value;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber.TickReceived += OnTickReceived;
        _subscriber.HealthReceived += OnHealthReceived;
        _subscriber.HeartbeatReceived += OnHeartbeatReceived;

        Log.Trace("INFO", $"[HealthMonitor] 健康监控已启动 (轮询间隔={_options.HealthPollIntervalSeconds}s, tick超时={_options.TickTimeoutSeconds}s, heartbeat超时={_options.HeartbeatTimeoutSeconds}s)");
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _subscriber.TickReceived -= OnTickReceived;
        _subscriber.HealthReceived -= OnHealthReceived;
        _subscriber.HeartbeatReceived -= OnHeartbeatReceived;

        Log.Trace("INFO", "[HealthMonitor] 健康监控已停止");
        return base.StopAsync(cancellationToken);
    }

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

    private static string FormatElapsed(DateTime? ts)
    {
        if (ts is null) return "n/a";
        var elapsed = DateTime.UtcNow - ts.Value;
        return $"{elapsed.TotalSeconds:F0}s ago";
    }

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

    private void OnTickReceived()
    {
        _lastTickReceived = DateTime.UtcNow;
    }

    private void OnHealthReceived(HealthNotification notification)
    {
        _lastHealthOk = notification.Ok;
        _lastHealthCheck = DateTime.UtcNow;
    }

    private void OnHeartbeatReceived(HeartbeatNotification notification)
    {
        _lastHeartbeatReceived = DateTime.UtcNow;
    }
}
