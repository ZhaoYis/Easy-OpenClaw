using OpenClaw.Core.Logging;

namespace OpenClaw.Core.Client;

/// <summary>
/// 为 <see cref="GatewayEventSubscriber"/> 的应用层 C# 事件批量注册「仅观测用」回调：
/// 输出触发了哪个事件以及参数摘要，不执行任何业务逻辑。
/// </summary>
public static class GatewayEventSubscriberExtensions
{
    /// <summary>
    /// 将 <see cref="GatewayEventSubscriber"/> 上全部应用层事件订阅为调试输出（事件名 + 参数）。
    /// 应在调用 <see cref="GatewayEventSubscriber.RegisterAll"/> 之前注册，以便与网关推送顺序一致。
    /// </summary>
    /// <remarks>仅用于观测与排障，不改变订阅器或网关状态。</remarks>
    /// <param name="subscriber">网关事件订阅器</param>
    /// <param name="emit">
    /// 每条应用回调的输出委托，参数为 (事件名, 参数字符串)；为 null 时使用 <see cref="Log.Debug"/>。
    /// </param>
    public static void RegisterAppLayerDebugCallbacks(this GatewayEventSubscriber subscriber,
        Action<string, string>? emit = null)
    {
        void Trace(string eventName, string args)
        {
            if (emit is not null)
                emit(eventName, args);
            else
                Log.Debug($"[app-callback] {eventName} → {args}");
        }

        subscriber.FirstDeltaReceived += () => Trace(nameof(GatewayEventSubscriber.FirstDeltaReceived), "(无参数)");
        subscriber.AgentDeltaReceived += d =>
            Trace(nameof(GatewayEventSubscriber.AgentDeltaReceived),
                $"length={d?.Length ?? 0}, preview={TruncateForTrace(d ?? "", 120)}");
        subscriber.ChatTurnCompleted += () => Trace(nameof(GatewayEventSubscriber.ChatTurnCompleted), "(无参数)");
        subscriber.ShutdownReceived += r =>
            Trace(nameof(GatewayEventSubscriber.ShutdownReceived), $"reason={FormatNullable(r)}");

        subscriber.ConnectChallengeReceived += n =>
            Trace(nameof(GatewayEventSubscriber.ConnectChallengeReceived), n.ToString());
        subscriber.AgentOtherStreamReceived += n =>
            Trace(nameof(GatewayEventSubscriber.AgentOtherStreamReceived), n.ToString());
        subscriber.ChatReceived += n =>
            Trace(nameof(GatewayEventSubscriber.ChatReceived), n.ToString());
        subscriber.ChatInjectReceived += n =>
            Trace(nameof(GatewayEventSubscriber.ChatInjectReceived), n.ToString());
        subscriber.SessionMessageReceived += n =>
            Trace(nameof(GatewayEventSubscriber.SessionMessageReceived), n.ToString());
        subscriber.SessionToolReceived += n =>
            Trace(nameof(GatewayEventSubscriber.SessionToolReceived), n.ToString());
        subscriber.SessionsChangedReceived += n =>
            Trace(nameof(GatewayEventSubscriber.SessionsChangedReceived), n.ToString());
        subscriber.PresenceReceived += n =>
            Trace(nameof(GatewayEventSubscriber.PresenceReceived), n.ToString());
        subscriber.TickReceived += () => Trace(nameof(GatewayEventSubscriber.TickReceived), "(无参数)");
        subscriber.TalkModeReceived += n =>
            Trace(nameof(GatewayEventSubscriber.TalkModeReceived), n.ToString());
        subscriber.HealthReceived += n =>
            Trace(nameof(GatewayEventSubscriber.HealthReceived), n.ToString());
        subscriber.HeartbeatReceived += n =>
            Trace(nameof(GatewayEventSubscriber.HeartbeatReceived), n.ToString());
        subscriber.CronReceived += n =>
            Trace(nameof(GatewayEventSubscriber.CronReceived), n.ToString());
        subscriber.NodePairRequestedReceived += n =>
            Trace(nameof(GatewayEventSubscriber.NodePairRequestedReceived), n.ToString());
        subscriber.NodePairResolvedReceived += n =>
            Trace(nameof(GatewayEventSubscriber.NodePairResolvedReceived), n.ToString());
        subscriber.NodeInvokeRequestReceived += n =>
            Trace(nameof(GatewayEventSubscriber.NodeInvokeRequestReceived), n.ToString());
        subscriber.DevicePairRequestedReceived += n =>
            Trace(nameof(GatewayEventSubscriber.DevicePairRequestedReceived), n.ToString());
        subscriber.DevicePairResolvedReceived += n =>
            Trace(nameof(GatewayEventSubscriber.DevicePairResolvedReceived), n.ToString());
        subscriber.VoicewakeChangedReceived += n =>
            Trace(nameof(GatewayEventSubscriber.VoicewakeChangedReceived), n.ToString());
        subscriber.ExecApprovalRequestedReceived += n =>
            Trace(nameof(GatewayEventSubscriber.ExecApprovalRequestedReceived), n.ToString());
        subscriber.ExecApprovalResolvedReceived += n =>
            Trace(nameof(GatewayEventSubscriber.ExecApprovalResolvedReceived), n.ToString());
        subscriber.PluginApprovalRequestedReceived += n =>
            Trace(nameof(GatewayEventSubscriber.PluginApprovalRequestedReceived), n.ToString());
        subscriber.PluginApprovalResolvedReceived += n =>
            Trace(nameof(GatewayEventSubscriber.PluginApprovalResolvedReceived), n.ToString());
        subscriber.UpdateAvailableReceived += n =>
            Trace(nameof(GatewayEventSubscriber.UpdateAvailableReceived), n.ToString());
        subscriber.UnknownGatewayEventReceived += n =>
            Trace(nameof(GatewayEventSubscriber.UnknownGatewayEventReceived), n.ToString());
    }

    /// <summary>
    /// 将可空字符串格式化为便于日志阅读的片段（null 显示为字面量 null）。
    /// </summary>
    private static string FormatNullable(string? value) => value ?? "null";

    /// <summary>
    /// 截断过长字符串，避免应用层 trace 刷屏。
    /// </summary>
    private static string TruncateForTrace(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "…";
}