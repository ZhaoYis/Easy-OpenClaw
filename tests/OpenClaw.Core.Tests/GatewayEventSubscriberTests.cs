using System.Collections.Generic;
using System.Text.Json;
using OpenClaw.Core.Client;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Models;
using OpenClaw.Core.Tests.Support;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="GatewayEventSubscriber"/> 的单元测试：<see cref="GatewayEventSubscriber.RegisterAll"/> 与各事件分支及 C# 事件回调。
/// </summary>
public sealed class GatewayEventSubscriberTests
{
    /// <summary>
    /// 注册全部处理器后，向 <see cref="GatewayClient.Events"/> 分发各已知事件应不抛异常，并触发部分 C# 事件。
    /// </summary>
    [Fact]
    public async Task RegisterAll_handlers_cover_protocol_events()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        var sub = new GatewayEventSubscriber(client);
        sub.RegisterAll();

        var first = false;
        string? delta = null;
        var turnDone = false;
        string? shutdown = null;
        sub.FirstDeltaReceived += () => first = true;
        sub.AgentDeltaReceived += d => delta = d;
        sub.ChatTurnCompleted += () => turnDone = true;
        sub.ShutdownReceived += r => shutdown = r;

        await Dispatch(client, GatewayConstants.Events.ConnectChallenge, new { nonce = "n", ts = "1" });
        await Dispatch(client, GatewayConstants.Events.Agent,
            new { stream = GatewayConstants.StreamTypes.Assistant, data = new { delta = "hi" } });
        await Dispatch(client, GatewayConstants.Events.Chat, new { state = GatewayConstants.ChatStates.Pending });
        await Dispatch(client, GatewayConstants.Events.Chat, new { state = GatewayConstants.ChatStates.Final });
        await Dispatch(client, GatewayConstants.Events.Agent, new { stream = "other", extra = 1 });
        await Dispatch(client, GatewayConstants.Events.ChatInject,
            new { sessionKey = "k", role = "user", messageId = "m1" });
        await Dispatch(client, GatewayConstants.Events.SessionMessage,
            new { sessionKey = "k", messageId = "mid", role = "assistant" });
        await Dispatch(client, GatewayConstants.Events.SessionTool,
            new { sessionKey = "k", toolCallId = "t1", toolName = "bash", phase = "start" });
        await Dispatch(client, GatewayConstants.Events.SessionsChanged, new { reason = "x", sessionKey = "s" });
        await Dispatch(client, GatewayConstants.Events.Presence,
            new { reason = "join", deviceId = "dev", mode = "cli", host = "h" });
        await Dispatch(client, GatewayConstants.Events.Tick, new { });
        await Dispatch(client, GatewayConstants.Events.TalkMode, new { mode = "ptt", active = true });
        await Dispatch(client, GatewayConstants.Events.Shutdown, new { reason = "restart" });
        await Dispatch(client, GatewayConstants.Events.Health,
            new { ok = true, channels = new { a = 1 }, agents = Array.Empty<object>() });
        await Dispatch(client, GatewayConstants.Events.Heartbeat, new { agentId = "ag", sessionKey = "sk" });
        await Dispatch(client, GatewayConstants.Events.Cron, new { action = "run", cronId = "c1" });
        await Dispatch(client, GatewayConstants.Events.NodePairRequested,
            new { requestId = "r1", nodeId = "n1", label = "l" });
        await Dispatch(client, GatewayConstants.Events.NodePairResolved,
            new { requestId = "r1", status = "ok", nodeId = "n1" });
        await Dispatch(client, GatewayConstants.Events.NodeInvokeRequest,
            new { invocationId = "i1", method = "m", nodeId = "n1" });
        await Dispatch(client, GatewayConstants.Events.DevicePairRequested,
            new { requestId = "d1", deviceId = "dev", platform = "mac" });
        await Dispatch(client, GatewayConstants.Events.DevicePairResolved,
            new { requestId = "d1", status = "approved", deviceId = "dev" });
        await Dispatch(client, GatewayConstants.Events.VoicewakeChanged, new { x = 1 });
        await Dispatch(client, GatewayConstants.Events.ExecApprovalRequested,
            new { approvalId = "a1", tool = "t", command = "c" });
        await Dispatch(client, GatewayConstants.Events.ExecApprovalResolved,
            new { approvalId = "a1", decision = "approve" });
        await Dispatch(client, GatewayConstants.Events.PluginApprovalRequested,
            new { approvalId = "p1", plugin = "pl", description = "d" });
        await Dispatch(client, GatewayConstants.Events.PluginApprovalResolved,
            new { approvalId = "p1", decision = "reject" });
        await Dispatch(client, GatewayConstants.Events.UpdateAvailable,
            new { currentVersion = "1", latestVersion = "2", channel = "stable" });

        await Dispatch(client, "custom.unknown", new { z = 1 });

        Assert.True(first);
        Assert.Equal("hi", delta);
        Assert.True(turnDone);
        Assert.Equal("restart", shutdown);
    }

    /// <summary>
    /// <see cref="GatewayEventSubscriber"/> 为各协议事件注册的强类型应用层回调均应在分发时触发且关键字段与 payload 一致。
    /// </summary>
    [Fact]
    public async Task RegisterAll_app_layer_callbacks_match_payloads()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        var sub = new GatewayEventSubscriber(client);
        sub.RegisterAll();

        ConnectChallengeNotification? cc = null;
        AgentOtherStreamNotification? agentOther = null;
        var chatList = new List<ChatNotification>();
        ChatInjectNotification? inj = null;
        SessionMessageNotification? sm = null;
        SessionToolNotification? st = null;
        SessionsChangedNotification? sch = null;
        PresenceNotification? pr = null;
        var tickCount = 0;
        TalkModeNotification? tm = null;
        HealthNotification? health = null;
        HeartbeatNotification? hb = null;
        CronNotification? cron = null;
        NodePairRequestedNotification? npr = null;
        NodePairResolvedNotification? npx = null;
        NodeInvokeRequestNotification? nir = null;
        DevicePairRequestedNotification? dpr = null;
        DevicePairResolvedNotification? dpx = null;
        VoicewakeChangedNotification? vw = null;
        ExecApprovalRequestedNotification? ear = null;
        ExecApprovalResolvedNotification? eax = null;
        PluginApprovalRequestedNotification? par = null;
        PluginApprovalResolvedNotification? pax = null;
        UpdateAvailableNotification? ua = null;
        UnknownGatewayEventNotification? unk = null;

        sub.ConnectChallengeReceived += n => cc = n;
        sub.AgentOtherStreamReceived += n => agentOther = n;
        sub.ChatReceived += n => chatList.Add(n);
        sub.ChatInjectReceived += n => inj = n;
        sub.SessionMessageReceived += n => sm = n;
        sub.SessionToolReceived += n => st = n;
        sub.SessionsChangedReceived += n => sch = n;
        sub.PresenceReceived += n => pr = n;
        sub.TickReceived += () => tickCount++;
        sub.TalkModeReceived += n => tm = n;
        sub.HealthReceived += n => health = n;
        sub.HeartbeatReceived += n => hb = n;
        sub.CronReceived += n => cron = n;
        sub.NodePairRequestedReceived += n => npr = n;
        sub.NodePairResolvedReceived += n => npx = n;
        sub.NodeInvokeRequestReceived += n => nir = n;
        sub.DevicePairRequestedReceived += n => dpr = n;
        sub.DevicePairResolvedReceived += n => dpx = n;
        sub.VoicewakeChangedReceived += n => vw = n;
        sub.ExecApprovalRequestedReceived += n => ear = n;
        sub.ExecApprovalResolvedReceived += n => eax = n;
        sub.PluginApprovalRequestedReceived += n => par = n;
        sub.PluginApprovalResolvedReceived += n => pax = n;
        sub.UpdateAvailableReceived += n => ua = n;
        sub.UnknownGatewayEventReceived += n => unk = n;

        await Dispatch(client, GatewayConstants.Events.ConnectChallenge, new { nonce = "nonce-full", ts = "99" });
        await Dispatch(client, GatewayConstants.Events.Agent,
            new { stream = GatewayConstants.StreamTypes.Assistant, data = new { delta = "x" } });
        await Dispatch(client, GatewayConstants.Events.Chat, new { state = GatewayConstants.ChatStates.Pending, sessionKey = "s0" });
        await Dispatch(client, GatewayConstants.Events.Chat,
            new { state = GatewayConstants.ChatStates.Final, sessionKey = "s0", kind = "k", type = "t" });
        await Dispatch(client, GatewayConstants.Events.Agent, new { stream = "other", extra = 1 });

        await Dispatch(client, GatewayConstants.Events.ChatInject,
            new { sessionKey = "k", role = "user", messageId = "m1" });
        await Dispatch(client, GatewayConstants.Events.SessionMessage,
            new { sessionKey = "k", messageId = "mid", role = "assistant" });
        await Dispatch(client, GatewayConstants.Events.SessionTool,
            new { sessionKey = "k", toolCallId = "t1", toolName = "bash", phase = "start" });
        await Dispatch(client, GatewayConstants.Events.SessionsChanged, new { reason = "x", sessionKey = "s" });
        await Dispatch(client, GatewayConstants.Events.Presence,
            new { reason = "join", deviceId = "dev", mode = "cli", host = "h" });
        await Dispatch(client, GatewayConstants.Events.Tick, new { });
        await Dispatch(client, GatewayConstants.Events.TalkMode, new { mode = "ptt", active = true });
        await Dispatch(client, GatewayConstants.Events.Shutdown, new { reason = "restart" });
        await Dispatch(client, GatewayConstants.Events.Health,
            new { ok = true, channels = new { a = 1 }, agents = Array.Empty<object>() });
        await Dispatch(client, GatewayConstants.Events.Heartbeat, new { agentId = "ag", sessionKey = "sk" });
        await Dispatch(client, GatewayConstants.Events.Cron, new { action = "run", cronId = "c1" });
        await Dispatch(client, GatewayConstants.Events.NodePairRequested,
            new { requestId = "r1", nodeId = "n1", label = "l" });
        await Dispatch(client, GatewayConstants.Events.NodePairResolved,
            new { requestId = "r1", status = "ok", nodeId = "n1" });
        await Dispatch(client, GatewayConstants.Events.NodeInvokeRequest,
            new { invocationId = "i1", method = "m", nodeId = "n1" });
        await Dispatch(client, GatewayConstants.Events.DevicePairRequested,
            new { requestId = "d1", deviceId = "dev", platform = "mac" });
        await Dispatch(client, GatewayConstants.Events.DevicePairResolved,
            new { requestId = "d1", status = "approved", deviceId = "dev" });
        await Dispatch(client, GatewayConstants.Events.VoicewakeChanged, new { x = 1 });
        await Dispatch(client, GatewayConstants.Events.ExecApprovalRequested,
            new { approvalId = "a1", tool = "t", command = "c" });
        await Dispatch(client, GatewayConstants.Events.ExecApprovalResolved,
            new { approvalId = "a1", decision = "approve" });
        await Dispatch(client, GatewayConstants.Events.PluginApprovalRequested,
            new { approvalId = "p1", plugin = "pl", description = "d" });
        await Dispatch(client, GatewayConstants.Events.PluginApprovalResolved,
            new { approvalId = "p1", decision = "reject" });
        await Dispatch(client, GatewayConstants.Events.UpdateAvailable,
            new { currentVersion = "1", latestVersion = "2", channel = "stable" });
        await Dispatch(client, "custom.unknown", new { z = 1 });

        Assert.NotNull(cc);
        Assert.Equal("nonce-full", cc.Value.Nonce);
        Assert.Equal("99", cc.Value.Ts);

        Assert.NotNull(agentOther);
        Assert.Equal("other", agentOther.Value.Stream);
        Assert.Contains("extra", agentOther.Value.PayloadKeysSummary);

        Assert.Equal(2, chatList.Count);
        Assert.Equal(GatewayConstants.ChatStates.Pending, chatList[0].State);
        Assert.Equal("s0", chatList[0].SessionKey);
        Assert.Equal(GatewayConstants.ChatStates.Final, chatList[1].State);
        Assert.Equal("k", chatList[1].Kind);
        Assert.Equal("t", chatList[1].Type);

        Assert.NotNull(inj);
        Assert.Equal("k", inj.Value.SessionKey);
        Assert.NotNull(sm);
        Assert.Equal("mid", sm.Value.MessageId);
        Assert.NotNull(st);
        Assert.Equal("bash", st.Value.ToolName);
        Assert.NotNull(sch);
        Assert.Equal("x", sch.Value.Reason);
        Assert.NotNull(pr);
        Assert.Equal("dev", pr.Value.DeviceId);
        Assert.Equal(1, tickCount);
        Assert.NotNull(tm);
        Assert.Equal("ptt", tm.Value.Mode);
        Assert.NotNull(health);
        Assert.True(health.Value.Ok);
        Assert.Equal(1, health.Value.ChannelCount);
        Assert.Equal(0, health.Value.AgentCount);
        Assert.NotNull(hb);
        Assert.Equal("ag", hb.Value.AgentId);
        Assert.NotNull(cron);
        Assert.Equal("c1", cron.Value.CronId);
        Assert.NotNull(npr);
        Assert.Equal("n1", npr.Value.NodeId);
        Assert.NotNull(npx);
        Assert.Equal("ok", npx.Value.Status);
        Assert.NotNull(nir);
        Assert.Equal("m", nir.Value.Method);
        Assert.NotNull(dpr);
        Assert.Equal("mac", dpr.Value.Platform);
        Assert.NotNull(dpx);
        Assert.Equal("approved", dpx.Value.Status);
        Assert.NotNull(vw);
        Assert.Contains("x", vw.Value.PayloadKeysSummary);
        Assert.NotNull(ear);
        Assert.Equal("a1", ear.Value.ApprovalId);
        Assert.NotNull(eax);
        Assert.Equal("approve", eax.Value.Decision);
        Assert.NotNull(par);
        Assert.Equal("pl", par.Value.Plugin);
        Assert.NotNull(pax);
        Assert.Equal("reject", pax.Value.Decision);
        Assert.NotNull(ua);
        Assert.Equal("2", ua.Value.LatestVersion);
        Assert.NotNull(unk);
        Assert.Equal("custom.unknown", unk.Value.EventName);
        Assert.NotNull(unk.Value.PayloadPreview);
    }

    /// <summary>
    /// 将任意对象序列化为 <see cref="GatewayEvent"/> 并交给路由器分发。
    /// </summary>
    private static async Task Dispatch(GatewayClient client, string name, object payload)
    {
        var el = JsonSerializer.SerializeToElement(payload, JsonDefaults.SerializerOptions);
        await client.Events.DispatchAsync(new GatewayEvent { Event = name, Payload = el });
    }
}
