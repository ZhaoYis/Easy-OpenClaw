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
    /// 将任意对象序列化为 <see cref="GatewayEvent"/> 并交给路由器分发。
    /// </summary>
    private static async Task Dispatch(GatewayClient client, string name, object payload)
    {
        var el = JsonSerializer.SerializeToElement(payload, JsonDefaults.SerializerOptions);
        await client.Events.DispatchAsync(new GatewayEvent { Event = name, Payload = el });
    }
}
