namespace OpenClaw.Core.Models;

/// <summary>
/// 网关健康状态快照，汇总主动探测和被动事件监听的结果。
/// 由 <see cref="OpenClaw.Core.Client.HealthMonitorService"/> 维护并在状态变更时推送给应用层。
/// </summary>
public sealed class GatewayHealthState
{
    /// <summary>综合健康判断：所有信号均正常时为 true</summary>
    public bool IsHealthy { get; init; }

    /// <summary>最近一次主动 health RPC 探测时间（UTC）</summary>
    public DateTime? LastHealthCheck { get; init; }

    /// <summary>最近一次 health RPC 返回的 ok 字段值</summary>
    public bool? LastHealthOk { get; init; }

    /// <summary>最近一次收到 tick 事件的时间（UTC）</summary>
    public DateTime? LastTickReceived { get; init; }

    /// <summary>最近一次收到 heartbeat 事件的时间（UTC）</summary>
    public DateTime? LastHeartbeatReceived { get; init; }

    /// <summary>当 <see cref="IsHealthy"/> 为 false 时的原因描述</summary>
    public string? UnhealthyReason { get; init; }

    /// <summary>WebSocket 连接是否处于活跃状态</summary>
    public bool IsConnected { get; init; }
}
