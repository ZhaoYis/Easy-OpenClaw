using System.Text.Json;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 网关事件订阅管理器。
/// 将所有服务端推送事件的注册逻辑集中管理，每个事件提取关键字段并打印到控制台。
/// 通过 C# event 回调通知调用方，实现事件处理逻辑与 UI/业务逻辑的解耦。
/// 事件名常量定义于 <see cref="GatewayConstants.Events"/>。
/// </summary>
public sealed class GatewayEventSubscriber
{
    private readonly GatewayClient _client;

    /// <summary>
    /// 初始化事件订阅管理器，绑定到指定的网关客户端实例。
    /// 创建后需调用 <see cref="RegisterAll"/> 完成所有事件处理器的注册。
    /// </summary>
    /// <param name="client">网关客户端，用于注册事件监听</param>
    public GatewayEventSubscriber(GatewayClient client)
    {
        _client = client;
    }

    // ═══════════════════════════════════════════════════════════
    //  应用层回调事件
    // ═══════════════════════════════════════════════════════════

    /// <summary>Agent 开始输出第一个 delta 文本块时触发（每轮对话仅触发一次）</summary>
    public event Action? FirstDeltaReceived;

    /// <summary>Agent 输出流式 delta 文本块时触发，参数为增量文本内容</summary>
    public event Action<string>? AgentDeltaReceived;

    /// <summary>聊天回合完成（state=final）时触发，表示 Agent 已完成本轮回复</summary>
    public event Action? ChatTurnCompleted;

    /// <summary>网关发送关闭通知时触发，参数为关闭原因（可能为 null）</summary>
    public event Action<string?>? ShutdownReceived;

    // ═══════════════════════════════════════════════════════════
    //  注册所有事件
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 一次性注册所有已知的网关事件处理器。
    /// 包括：连接挑战、Agent 流式输出、聊天与 chat.inject、会话 transcript（session.message / session.tool）、
    /// sessions.changed、在线状态、tick、定时任务、节点/设备配对、语音唤醒、执行/插件审批、系统更新等事件。
    /// 最后注册通配符处理器，用于捕获未知事件类型并记录日志。
    /// </summary>
    public void RegisterAll()
    {
        RegisterConnectChallenge();
        RegisterAgent();
        RegisterChat();
        RegisterChatInject();
        RegisterSessionMessage();
        RegisterSessionTool();
        RegisterSessionsChanged();
        RegisterPresence();
        RegisterTick();
        RegisterTalkMode();
        RegisterShutdown();
        RegisterHealth();
        RegisterHeartbeat();
        RegisterCron();
        RegisterNodePairRequested();
        RegisterNodePairResolved();
        RegisterNodeInvokeRequest();
        RegisterDevicePairRequested();
        RegisterDevicePairResolved();
        RegisterVoicewakeChanged();
        RegisterExecApprovalRequested();
        RegisterExecApprovalResolved();
        RegisterPluginApprovalRequested();
        RegisterPluginApprovalResolved();
        RegisterUpdateAvailable();
        RegisterWildcard();
    }

    // ═══════════════════════════════════════════════════════════
    //  各事件处理器
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 注册 connect_challenge 事件处理器。
    /// 服务端在 WebSocket 连接建立后下发此事件，包含 nonce 和时间戳，用于设备身份验证。
    /// </summary>
    private void RegisterConnectChallenge()
    {
        _client.OnEvent(GatewayConstants.Events.ConnectChallenge, evt =>
        {
            var nonce = GetString(evt, "nonce");
            var ts = GetString(evt, "ts");
            Log.Event(GatewayConstants.Events.ConnectChallenge, $"nonce={Truncate(nonce, 16)}, ts={ts}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 agent 事件处理器。处理 Agent 的流式输出：
    /// - 当 stream=assistant 且包含 delta 文本时，触发 <see cref="AgentDeltaReceived"/> 回调
    /// - 首次收到 delta 时额外触发 <see cref="FirstDeltaReceived"/>
    /// - 同时监听 chat 事件的 pending/streaming 状态重置 firstDelta 标志
    /// </summary>
    private void RegisterAgent()
    {
        var firstDelta = false;

        _client.OnEvent(GatewayConstants.Events.Agent, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var stream = p.TryGetProperty("stream", out var s) ? s.GetString() : null;

            if (stream == GatewayConstants.StreamTypes.Assistant && p.TryGetProperty("data", out var data)
                                      && data.TryGetProperty("delta", out var delta))
            {
                var text = delta.GetString() ?? "";

                if (!firstDelta)
                {
                    firstDelta = true;
                    FirstDeltaReceived?.Invoke();
                }

                AgentDeltaReceived?.Invoke(text);
            }
            else
            {
                Log.Event(GatewayConstants.Events.Agent, $"stream={stream ?? "?"}, keys=[{GetKeys(p)}]");
            }

            return Task.CompletedTask;
        });

        _client.OnEvent(GatewayConstants.Events.Chat, evt =>
        {
            if (evt.Payload is { } p)
            {
                var state = p.TryGetProperty("state", out var st) ? st.GetString() : null;
                if (state is GatewayConstants.ChatStates.Pending or GatewayConstants.ChatStates.Streaming)
                    firstDelta = false;
            }
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 chat 事件处理器。监听聊天状态变更（pending → streaming → final），
    /// 同时识别同属 chat 事件族的 transcript 子类型（例如 kind/type 为 inject 的 UI 注入），
    /// 当状态变为 final 时触发 <see cref="ChatTurnCompleted"/> 回调，通知调用方本轮对话已完成。
    /// </summary>
    private void RegisterChat()
    {
        _client.OnEvent(GatewayConstants.Events.Chat, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var state = p.TryGetProperty("state", out var st) ? st.GetString() : null;
            var sessionKey = p.TryGetProperty("sessionKey", out var sk) ? sk.GetString() : null;
            // 部分网关在同一 event=chat 下用 kind 或 type 区分 inject 等 transcript 子事件
            var kind = p.TryGetProperty("kind", out var k) ? k.GetString() : null;
            var type = p.TryGetProperty("type", out var tp) ? tp.GetString() : null;

            Log.Event(GatewayConstants.Events.Chat,
                $"state={state}, session={sessionKey}, kind={kind ?? "-"}, type={type ?? "-"}");

            if (state == GatewayConstants.ChatStates.Final)
                ChatTurnCompleted?.Invoke();

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 chat.inject 事件处理器。UI 向 transcript 注入消息等场景下网关可能单独推送此事件名。
    /// </summary>
    private void RegisterChatInject()
    {
        _client.OnEvent(GatewayConstants.Events.ChatInject, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var sessionKey = p.TryGetProperty("sessionKey", out var sk) ? sk.GetString() : null;
            var role = p.TryGetProperty("role", out var r) ? r.GetString() : null;
            var messageId = p.TryGetProperty("messageId", out var m) ? m.GetString() : null;

            Log.Event(GatewayConstants.Events.ChatInject,
                $"session={sessionKey}, role={role}, messageId={Truncate(messageId ?? "", 24)}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 session.message 事件处理器。订阅会话后收到 transcript 消息新增或更新。
    /// </summary>
    private void RegisterSessionMessage()
    {
        _client.OnEvent(GatewayConstants.Events.SessionMessage, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var sessionKey = p.TryGetProperty("sessionKey", out var sk) ? sk.GetString() : null;
            var messageId = p.TryGetProperty("messageId", out var mid) ? mid.GetString() : null;
            var role = p.TryGetProperty("role", out var r) ? r.GetString() : null;

            Log.Event(GatewayConstants.Events.SessionMessage,
                $"session={sessionKey}, messageId={Truncate(messageId ?? "", 24)}, role={role}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 session.tool 事件处理器。订阅会话后收到工具调用或工具流相关事件流片段。
    /// </summary>
    private void RegisterSessionTool()
    {
        _client.OnEvent(GatewayConstants.Events.SessionTool, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var sessionKey = p.TryGetProperty("sessionKey", out var sk) ? sk.GetString() : null;
            var toolCallId = p.TryGetProperty("toolCallId", out var tc) ? tc.GetString() : null;
            var toolName = p.TryGetProperty("toolName", out var tn) ? tn.GetString() : null;
            var phase = p.TryGetProperty("phase", out var ph) ? ph.GetString() : null;

            Log.Event(GatewayConstants.Events.SessionTool,
                $"session={sessionKey}, toolCallId={Truncate(toolCallId ?? "", 20)}, tool={toolName}, phase={phase}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 sessions.changed 事件处理器。会话列表索引或会话元数据发生变化时推送。
    /// </summary>
    private void RegisterSessionsChanged()
    {
        _client.OnEvent(GatewayConstants.Events.SessionsChanged, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var reason = p.TryGetProperty("reason", out var r) ? r.GetString() : null;
            var sessionKey = p.TryGetProperty("sessionKey", out var sk) ? sk.GetString() : null;

            Log.Event(GatewayConstants.Events.SessionsChanged,
                $"reason={reason}, session={sessionKey}, keys=[{GetKeys(p)}]");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 presence 事件处理器。监听设备上下线变化，
    /// 记录变化原因、设备 ID、连接模式和主机名等信息。
    /// </summary>
    private void RegisterPresence()
    {
        _client.OnEvent(GatewayConstants.Events.Presence, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var reason = p.TryGetProperty("reason", out var r) ? r.GetString() : null;
            var deviceId = p.TryGetProperty("deviceId", out var d) ? d.GetString() : null;
            var mode = p.TryGetProperty("mode", out var m) ? m.GetString() : null;
            var host = p.TryGetProperty("host", out var h) ? h.GetString() : null;

            Log.Event(GatewayConstants.Events.Presence, $"reason={reason}, device={Truncate(deviceId ?? "", 12)}, mode={mode}, host={host}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 tick 事件处理器。tick 为网关周期性心跳保活信号，仅记录 debug 日志。
    /// </summary>
    private void RegisterTick()
    {
        _client.OnEvent(GatewayConstants.Events.Tick, _ =>
        {
            Log.Debug("tick");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 talk_mode 事件处理器。监听语音对话模式切换（如 push-to-talk ↔ hands-free）。
    /// </summary>
    private void RegisterTalkMode()
    {
        _client.OnEvent(GatewayConstants.Events.TalkMode, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var mode = p.TryGetProperty("mode", out var m) ? m.GetString() : null;
            var active = p.TryGetProperty("active", out var a) ? a.ToString() : null;

            Log.Event(GatewayConstants.Events.TalkMode, $"mode={mode}, active={active}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 shutdown 事件处理器。网关即将关闭时收到此事件，
    /// 记录警告日志并触发 <see cref="ShutdownReceived"/> 回调，通知应用层做清理准备。
    /// </summary>
    private void RegisterShutdown()
    {
        _client.OnEvent(GatewayConstants.Events.Shutdown, evt =>
        {
            var reason = evt.Payload is { } p && p.TryGetProperty("reason", out var r)
                ? r.GetString() : null;

            Log.Warn($"Gateway 正在关闭 (reason={reason ?? "unknown"})");
            ShutdownReceived?.Invoke(reason);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 health 事件处理器。监听网关健康状态变化，
    /// 记录整体状态（ok/fail）、渠道数量和 Agent 数量。
    /// </summary>
    private void RegisterHealth()
    {
        _client.OnEvent(GatewayConstants.Events.Health, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var ok = p.TryGetProperty("ok", out var o) ? o.GetBoolean().ToString() : "?";
            var channels = p.TryGetProperty("channels", out var ch) ? ch.EnumerateObject().Count().ToString() : "0";
            var agents = p.TryGetProperty("agents", out var ag) && ag.ValueKind == JsonValueKind.Array
                ? ag.GetArrayLength().ToString() : "0";

            Log.Event(GatewayConstants.Events.Health, $"ok={ok}, channels={channels}, agents={agents}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 heartbeat 事件处理器。记录 Agent 心跳信息（关联的 agentId 和 sessionKey）。
    /// </summary>
    private void RegisterHeartbeat()
    {
        _client.OnEvent(GatewayConstants.Events.Heartbeat, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var agentId = p.TryGetProperty("agentId", out var a) ? a.GetString() : null;
            var sessionKey = p.TryGetProperty("sessionKey", out var sk) ? sk.GetString() : null;

            Log.Event(GatewayConstants.Events.Heartbeat, $"agent={agentId}, session={sessionKey}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 cron 事件处理器。监听定时任务的执行和状态变化，记录操作类型和任务 ID。
    /// </summary>
    private void RegisterCron()
    {
        _client.OnEvent(GatewayConstants.Events.Cron, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var action = p.TryGetProperty("action", out var a) ? a.GetString() : null;
            var cronId = p.TryGetProperty("cronId", out var c) ? c.GetString() : null;

            Log.Event(GatewayConstants.Events.Cron, $"action={action}, cronId={cronId}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 node_pair_requested 事件处理器。当有新节点发起配对请求时收到此事件，
    /// 记录请求 ID、节点 ID 和标签信息。
    /// </summary>
    private void RegisterNodePairRequested()
    {
        _client.OnEvent(GatewayConstants.Events.NodePairRequested, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var requestId = p.TryGetProperty("requestId", out var r) ? r.GetString() : null;
            var nodeId = p.TryGetProperty("nodeId", out var n) ? n.GetString() : null;
            var label = p.TryGetProperty("label", out var l) ? l.GetString() : null;

            Log.Event(GatewayConstants.Events.NodePairRequested, $"requestId={requestId}, nodeId={Truncate(nodeId ?? "", 12)}, label={label}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 node_pair_resolved 事件处理器。当节点配对请求被批准或拒绝时收到此事件，
    /// 记录请求 ID、审批状态和节点 ID。
    /// </summary>
    private void RegisterNodePairResolved()
    {
        _client.OnEvent(GatewayConstants.Events.NodePairResolved, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var requestId = p.TryGetProperty("requestId", out var r) ? r.GetString() : null;
            var status = p.TryGetProperty("status", out var s) ? s.GetString() : null;
            var nodeId = p.TryGetProperty("nodeId", out var n) ? n.GetString() : null;

            Log.Event(GatewayConstants.Events.NodePairResolved, $"requestId={requestId}, status={status}, nodeId={Truncate(nodeId ?? "", 12)}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 node_invoke_request 事件处理器。当网关向节点下发能力调用请求时收到此事件，
    /// 记录调用 ID、方法名和目标节点 ID。
    /// </summary>
    private void RegisterNodeInvokeRequest()
    {
        _client.OnEvent(GatewayConstants.Events.NodeInvokeRequest, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var invocationId = p.TryGetProperty("invocationId", out var i) ? i.GetString() : null;
            var method = p.TryGetProperty("method", out var m) ? m.GetString() : null;
            var nodeId = p.TryGetProperty("nodeId", out var n) ? n.GetString() : null;

            Log.Event(GatewayConstants.Events.NodeInvokeRequest, $"invId={Truncate(invocationId ?? "", 12)}, method={method}, node={Truncate(nodeId ?? "", 12)}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 device_pair_requested 事件处理器。当有新设备发起配对请求时收到此事件，
    /// 记录请求 ID、设备 ID 和平台类型。
    /// </summary>
    private void RegisterDevicePairRequested()
    {
        _client.OnEvent(GatewayConstants.Events.DevicePairRequested, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var requestId = p.TryGetProperty("requestId", out var r) ? r.GetString() : null;
            var deviceId = p.TryGetProperty("deviceId", out var d) ? d.GetString() : null;
            var platform = p.TryGetProperty("platform", out var pl) ? pl.GetString() : null;

            Log.Event(GatewayConstants.Events.DevicePairRequested, $"requestId={requestId}, device={Truncate(deviceId ?? "", 12)}, platform={platform}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 device_pair_resolved 事件处理器。当设备配对请求被批准或拒绝时收到此事件，
    /// 记录请求 ID、审批状态和设备 ID。
    /// </summary>
    private void RegisterDevicePairResolved()
    {
        _client.OnEvent(GatewayConstants.Events.DevicePairResolved, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var requestId = p.TryGetProperty("requestId", out var r) ? r.GetString() : null;
            var status = p.TryGetProperty("status", out var s) ? s.GetString() : null;
            var deviceId = p.TryGetProperty("deviceId", out var d) ? d.GetString() : null;

            Log.Event(GatewayConstants.Events.DevicePairResolved, $"requestId={requestId}, status={status}, device={Truncate(deviceId ?? "", 12)}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 voicewake_changed 事件处理器。当语音唤醒配置变更时收到此事件，记录 payload 键名。
    /// </summary>
    private void RegisterVoicewakeChanged()
    {
        _client.OnEvent(GatewayConstants.Events.VoicewakeChanged, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;
            Log.Event(GatewayConstants.Events.VoicewakeChanged, $"payload=[{GetKeys(p)}]");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 exec_approval_requested 事件处理器。当 Agent 请求执行需要审批的工具时收到此事件，
    /// 记录审批 ID、工具名称和命令内容。
    /// </summary>
    private void RegisterExecApprovalRequested()
    {
        _client.OnEvent(GatewayConstants.Events.ExecApprovalRequested, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var approvalId = p.TryGetProperty("approvalId", out var a) ? a.GetString() : null;
            var tool = p.TryGetProperty("tool", out var t) ? t.GetString() : null;
            var command = p.TryGetProperty("command", out var c) ? c.GetString() : null;

            Log.Event(GatewayConstants.Events.ExecApprovalRequested, $"approvalId={approvalId}, tool={tool}, command={Truncate(command ?? "", 60)}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 exec_approval_resolved 事件处理器。当执行审批请求被批准或拒绝时收到此事件，
    /// 记录审批 ID 和决定结果。
    /// </summary>
    private void RegisterExecApprovalResolved()
    {
        _client.OnEvent(GatewayConstants.Events.ExecApprovalResolved, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var approvalId = p.TryGetProperty("approvalId", out var a) ? a.GetString() : null;
            var decision = p.TryGetProperty("decision", out var d) ? d.GetString() : null;

            Log.Event(GatewayConstants.Events.ExecApprovalResolved, $"approvalId={approvalId}, decision={decision}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 plugin_approval_requested 事件处理器。当插件发起自定义审批请求时收到此事件，
    /// 记录审批 ID、插件名称和操作描述。
    /// </summary>
    private void RegisterPluginApprovalRequested()
    {
        _client.OnEvent(GatewayConstants.Events.PluginApprovalRequested, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var approvalId = p.TryGetProperty("approvalId", out var a) ? a.GetString() : null;
            var plugin = p.TryGetProperty("plugin", out var pl) ? pl.GetString() : null;
            var description = p.TryGetProperty("description", out var d) ? d.GetString() : null;

            Log.Event(GatewayConstants.Events.PluginApprovalRequested, $"approvalId={approvalId}, plugin={plugin}, description={Truncate(description ?? "", 60)}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 plugin_approval_resolved 事件处理器。当插件审批请求被批准或拒绝时收到此事件，
    /// 记录审批 ID 和决定结果。
    /// </summary>
    private void RegisterPluginApprovalResolved()
    {
        _client.OnEvent(GatewayConstants.Events.PluginApprovalResolved, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var approvalId = p.TryGetProperty("approvalId", out var a) ? a.GetString() : null;
            var decision = p.TryGetProperty("decision", out var d) ? d.GetString() : null;

            Log.Event(GatewayConstants.Events.PluginApprovalResolved, $"approvalId={approvalId}, decision={decision}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册 update_available 事件处理器。当网关检测到可用更新时收到此事件，
    /// 以警告级别记录当前版本、最新版本和更新渠道。
    /// </summary>
    private void RegisterUpdateAvailable()
    {
        _client.OnEvent(GatewayConstants.Events.UpdateAvailable, evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var current = p.TryGetProperty("currentVersion", out var cv) ? cv.GetString() : null;
            var latest = p.TryGetProperty("latestVersion", out var lv) ? lv.GetString() : null;
            var channel = p.TryGetProperty("channel", out var ch) ? ch.GetString() : null;

            Log.Warn($"[{GatewayConstants.Events.UpdateAvailable}] {current} → {latest} (channel={channel})");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 注册通配符 "*" 事件处理器。捕获所有未在 <see cref="KnownEvents"/> 中注册的未知事件类型，
    /// 将其 payload 摘要记录到日志，便于发现网关新增的事件类型。
    /// </summary>
    private void RegisterWildcard()
    {
        _client.Events.On(GatewayConstants.Events.Wildcard, evt =>
        {
            if (!KnownEvents.Contains(evt.Event))
                Log.Event($"[unknown] {evt.Event}", evt.Payload?.GetRawText() is { } r ? Truncate(r, 120) : "");
            return Task.CompletedTask;
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  内部工具方法
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 已知事件名称集合，用于通配符处理器过滤。
    /// 已在专用处理器中注册的事件不会被通配符处理器重复记录。
    /// </summary>
    private static readonly HashSet<string> KnownEvents =
    [
        GatewayConstants.Events.ConnectChallenge,
        GatewayConstants.Events.Agent,
        GatewayConstants.Events.Chat,
        GatewayConstants.Events.ChatInject,
        GatewayConstants.Events.SessionMessage,
        GatewayConstants.Events.SessionTool,
        GatewayConstants.Events.SessionsChanged,
        GatewayConstants.Events.Presence,
        GatewayConstants.Events.Tick,
        GatewayConstants.Events.TalkMode,
        GatewayConstants.Events.Shutdown,
        GatewayConstants.Events.Health,
        GatewayConstants.Events.Heartbeat,
        GatewayConstants.Events.Cron,
        GatewayConstants.Events.NodePairRequested,
        GatewayConstants.Events.NodePairResolved,
        GatewayConstants.Events.NodeInvokeRequest,
        GatewayConstants.Events.DevicePairRequested,
        GatewayConstants.Events.DevicePairResolved,
        GatewayConstants.Events.VoicewakeChanged,
        GatewayConstants.Events.ExecApprovalRequested,
        GatewayConstants.Events.ExecApprovalResolved,
        GatewayConstants.Events.PluginApprovalRequested,
        GatewayConstants.Events.PluginApprovalResolved,
        GatewayConstants.Events.UpdateAvailable,
    ];

    /// <summary>
    /// 从事件 payload 中安全提取指定属性的字符串值。
    /// </summary>
    /// <param name="evt">网关事件</param>
    /// <param name="prop">属性名</param>
    /// <returns>属性值字符串，不存在时返回空字符串</returns>
    private static string GetString(GatewayEvent evt, string prop)
    {
        if (evt.Payload is { } p && p.TryGetProperty(prop, out var val))
            return val.ToString();
        return "";
    }

    /// <summary>
    /// 提取 JSON 对象的所有顶层属性名，用逗号分隔。
    /// 非对象类型返回其 ValueKind 名称。用于日志中快速展示 payload 结构。
    /// </summary>
    /// <param name="el">JSON 元素</param>
    /// <returns>逗号分隔的属性名列表，或 ValueKind 名称</returns>
    private static string GetKeys(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return el.ValueKind.ToString();
        return string.Join(", ", el.EnumerateObject().Select(p => p.Name));
    }

    /// <summary>
    /// 截断字符串到指定最大长度，超出部分以 "…" 替代。用于日志中避免输出过长内容。
    /// </summary>
    /// <param name="s">原始字符串</param>
    /// <param name="maxLen">最大保留字符数</param>
    /// <returns>截断后的字符串</returns>
    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "…";
}
