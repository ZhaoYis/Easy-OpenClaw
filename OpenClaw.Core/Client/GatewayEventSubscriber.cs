using System.Text.Json;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 网关事件订阅管理器。
/// 将所有服务端推送事件的注册逻辑集中管理，每个事件提取关键字段并打印到控制台。
/// 通过 C# event 回调通知调用方，实现事件处理逻辑与 UI/业务逻辑的解耦。
/// </summary>
public sealed class OpenClawGatewayEventSubscriber
{
    private readonly GatewayClient _client;

    public OpenClawGatewayEventSubscriber(GatewayClient client)
    {
        _client = client;
    }

    // ═══════════════════════════════════════════════════════════
    //  应用层回调事件
    // ═══════════════════════════════════════════════════════════

    public event Action? FirstDeltaReceived;
    public event Action<string>? AgentDeltaReceived;
    public event Action? ChatTurnCompleted;
    public event Action<string?>? ShutdownReceived;

    // ═══════════════════════════════════════════════════════════
    //  注册所有事件
    // ═══════════════════════════════════════════════════════════

    public void RegisterAll()
    {
        RegisterConnectChallenge();
        RegisterAgent();
        RegisterChat();
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
        RegisterUpdateAvailable();
        RegisterWildcard();
    }

    // ═══════════════════════════════════════════════════════════
    //  各事件处理器
    // ═══════════════════════════════════════════════════════════

    private void RegisterConnectChallenge()
    {
        _client.OnEvent("connect.challenge", evt =>
        {
            var nonce = GetString(evt, "nonce");
            var ts = GetString(evt, "ts");
            Log.Event("connect.challenge", $"nonce={Truncate(nonce, 16)}, ts={ts}");
            return Task.CompletedTask;
        });
    }

    private void RegisterAgent()
    {
        var firstDelta = false;

        _client.OnEvent("agent", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var stream = p.TryGetProperty("stream", out var s) ? s.GetString() : null;

            if (stream == "assistant" && p.TryGetProperty("data", out var data)
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
                Log.Event("agent", $"stream={stream ?? "?"}, keys=[{GetKeys(p)}]");
            }

            return Task.CompletedTask;
        });

        _client.OnEvent("chat", evt =>
        {
            if (evt.Payload is { } p)
            {
                var state = p.TryGetProperty("state", out var st) ? st.GetString() : null;
                if (state is "pending" or "streaming")
                    firstDelta = false;
            }
            return Task.CompletedTask;
        });
    }

    private void RegisterChat()
    {
        _client.OnEvent("chat", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var state = p.TryGetProperty("state", out var st) ? st.GetString() : null;
            var sessionKey = p.TryGetProperty("sessionKey", out var sk) ? sk.GetString() : null;

            Log.Event("chat", $"state={state}, session={sessionKey}");

            if (state == "final")
                ChatTurnCompleted?.Invoke();

            return Task.CompletedTask;
        });
    }

    private void RegisterPresence()
    {
        _client.OnEvent("presence", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var reason = p.TryGetProperty("reason", out var r) ? r.GetString() : null;
            var deviceId = p.TryGetProperty("deviceId", out var d) ? d.GetString() : null;
            var mode = p.TryGetProperty("mode", out var m) ? m.GetString() : null;
            var host = p.TryGetProperty("host", out var h) ? h.GetString() : null;

            Log.Event("presence", $"reason={reason}, device={Truncate(deviceId ?? "", 12)}, mode={mode}, host={host}");
            return Task.CompletedTask;
        });
    }

    private void RegisterTick()
    {
        _client.OnEvent("tick", _ =>
        {
            Log.Debug("tick");
            return Task.CompletedTask;
        });
    }

    private void RegisterTalkMode()
    {
        _client.OnEvent("talk.mode", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var mode = p.TryGetProperty("mode", out var m) ? m.GetString() : null;
            var active = p.TryGetProperty("active", out var a) ? a.ToString() : null;

            Log.Event("talk.mode", $"mode={mode}, active={active}");
            return Task.CompletedTask;
        });
    }

    private void RegisterShutdown()
    {
        _client.OnEvent("shutdown", evt =>
        {
            var reason = evt.Payload is { } p && p.TryGetProperty("reason", out var r)
                ? r.GetString() : null;

            Log.Warn($"Gateway 正在关闭 (reason={reason ?? "unknown"})");
            ShutdownReceived?.Invoke(reason);
            return Task.CompletedTask;
        });
    }

    private void RegisterHealth()
    {
        _client.OnEvent("health", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var ok = p.TryGetProperty("ok", out var o) ? o.GetBoolean().ToString() : "?";
            var channels = p.TryGetProperty("channels", out var ch) ? ch.EnumerateObject().Count().ToString() : "0";
            var agents = p.TryGetProperty("agents", out var ag) && ag.ValueKind == JsonValueKind.Array
                ? ag.GetArrayLength().ToString() : "0";

            Log.Event("health", $"ok={ok}, channels={channels}, agents={agents}");
            return Task.CompletedTask;
        });
    }

    private void RegisterHeartbeat()
    {
        _client.OnEvent("heartbeat", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var agentId = p.TryGetProperty("agentId", out var a) ? a.GetString() : null;
            var sessionKey = p.TryGetProperty("sessionKey", out var sk) ? sk.GetString() : null;

            Log.Event("heartbeat", $"agent={agentId}, session={sessionKey}");
            return Task.CompletedTask;
        });
    }

    private void RegisterCron()
    {
        _client.OnEvent("cron", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var action = p.TryGetProperty("action", out var a) ? a.GetString() : null;
            var cronId = p.TryGetProperty("cronId", out var c) ? c.GetString() : null;

            Log.Event("cron", $"action={action}, cronId={cronId}");
            return Task.CompletedTask;
        });
    }

    private void RegisterNodePairRequested()
    {
        _client.OnEvent("node.pair.requested", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var requestId = p.TryGetProperty("requestId", out var r) ? r.GetString() : null;
            var nodeId = p.TryGetProperty("nodeId", out var n) ? n.GetString() : null;
            var label = p.TryGetProperty("label", out var l) ? l.GetString() : null;

            Log.Event("node.pair.requested", $"requestId={requestId}, nodeId={Truncate(nodeId ?? "", 12)}, label={label}");
            return Task.CompletedTask;
        });
    }

    private void RegisterNodePairResolved()
    {
        _client.OnEvent("node.pair.resolved", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var requestId = p.TryGetProperty("requestId", out var r) ? r.GetString() : null;
            var status = p.TryGetProperty("status", out var s) ? s.GetString() : null;
            var nodeId = p.TryGetProperty("nodeId", out var n) ? n.GetString() : null;

            Log.Event("node.pair.resolved", $"requestId={requestId}, status={status}, nodeId={Truncate(nodeId ?? "", 12)}");
            return Task.CompletedTask;
        });
    }

    private void RegisterNodeInvokeRequest()
    {
        _client.OnEvent("node.invoke.request", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var invocationId = p.TryGetProperty("invocationId", out var i) ? i.GetString() : null;
            var method = p.TryGetProperty("method", out var m) ? m.GetString() : null;
            var nodeId = p.TryGetProperty("nodeId", out var n) ? n.GetString() : null;

            Log.Event("node.invoke.request", $"invId={Truncate(invocationId ?? "", 12)}, method={method}, node={Truncate(nodeId ?? "", 12)}");
            return Task.CompletedTask;
        });
    }

    private void RegisterDevicePairRequested()
    {
        _client.OnEvent("device.pair.requested", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var requestId = p.TryGetProperty("requestId", out var r) ? r.GetString() : null;
            var deviceId = p.TryGetProperty("deviceId", out var d) ? d.GetString() : null;
            var platform = p.TryGetProperty("platform", out var pl) ? pl.GetString() : null;

            Log.Event("device.pair.requested", $"requestId={requestId}, device={Truncate(deviceId ?? "", 12)}, platform={platform}");
            return Task.CompletedTask;
        });
    }

    private void RegisterDevicePairResolved()
    {
        _client.OnEvent("device.pair.resolved", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var requestId = p.TryGetProperty("requestId", out var r) ? r.GetString() : null;
            var status = p.TryGetProperty("status", out var s) ? s.GetString() : null;
            var deviceId = p.TryGetProperty("deviceId", out var d) ? d.GetString() : null;

            Log.Event("device.pair.resolved", $"requestId={requestId}, status={status}, device={Truncate(deviceId ?? "", 12)}");
            return Task.CompletedTask;
        });
    }

    private void RegisterVoicewakeChanged()
    {
        _client.OnEvent("voicewake.changed", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;
            Log.Event("voicewake.changed", $"payload=[{GetKeys(p)}]");
            return Task.CompletedTask;
        });
    }

    private void RegisterExecApprovalRequested()
    {
        _client.OnEvent("exec.approval.requested", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var approvalId = p.TryGetProperty("approvalId", out var a) ? a.GetString() : null;
            var tool = p.TryGetProperty("tool", out var t) ? t.GetString() : null;
            var command = p.TryGetProperty("command", out var c) ? c.GetString() : null;

            Log.Event("exec.approval.requested", $"approvalId={approvalId}, tool={tool}, command={Truncate(command ?? "", 60)}");
            return Task.CompletedTask;
        });
    }

    private void RegisterExecApprovalResolved()
    {
        _client.OnEvent("exec.approval.resolved", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var approvalId = p.TryGetProperty("approvalId", out var a) ? a.GetString() : null;
            var decision = p.TryGetProperty("decision", out var d) ? d.GetString() : null;

            Log.Event("exec.approval.resolved", $"approvalId={approvalId}, decision={decision}");
            return Task.CompletedTask;
        });
    }

    private void RegisterUpdateAvailable()
    {
        _client.OnEvent("update.available", evt =>
        {
            if (evt.Payload is not { } p) return Task.CompletedTask;

            var current = p.TryGetProperty("currentVersion", out var cv) ? cv.GetString() : null;
            var latest = p.TryGetProperty("latestVersion", out var lv) ? lv.GetString() : null;
            var channel = p.TryGetProperty("channel", out var ch) ? ch.GetString() : null;

            Log.Warn($"[update.available] {current} → {latest} (channel={channel})");
            return Task.CompletedTask;
        });
    }

    private void RegisterWildcard()
    {
        _client.Events.On("*", evt =>
        {
            if (!KnownEvents.Contains(evt.Event))
                Log.Event($"[unknown] {evt.Event}", evt.Payload?.GetRawText() is { } r ? Truncate(r, 120) : "");
            return Task.CompletedTask;
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  内部工具方法
    // ═══════════════════════════════════════════════════════════

    private static readonly HashSet<string> KnownEvents =
    [
        "connect.challenge", "agent", "chat", "presence", "tick",
        "talk.mode", "shutdown", "health", "heartbeat", "cron",
        "node.pair.requested", "node.pair.resolved", "node.invoke.request",
        "device.pair.requested", "device.pair.resolved",
        "voicewake.changed", "exec.approval.requested", "exec.approval.resolved",
        "update.available",
    ];

    private static string GetString(GatewayEvent evt, string prop)
    {
        if (evt.Payload is { } p && p.TryGetProperty(prop, out var val))
            return val.ToString();
        return "";
    }

    private static string GetKeys(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return el.ValueKind.ToString();
        return string.Join(", ", el.EnumerateObject().Select(p => p.Name));
    }

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "…";
}
