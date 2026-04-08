using System.Collections.Generic;
using System.Text.Json;
using OpenClaw.Core.Client;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Models;
using OpenClaw.Core.Tests.Support;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="GatewayEventSubscriberExtensions.RegisterAppLayerDebugCallbacks"/> 的单元测试：
/// 验证应用层回调仅做观测输出且事件名与参数可被捕获。
/// </summary>
public sealed class GatewayEventSubscriberExtensionsTests
{
    /// <summary>
    /// 注册调试回调后分发若干网关事件，应产生对应事件名且强类型参数可被 ToString 还原。
    /// </summary>
    [Fact]
    public async Task RegisterAppLayerDebugCallbacks_emits_event_names_and_args()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        var sub = new GatewayEventSubscriber(client);
        var lines = new List<(string Name, string Args)>();
        sub.RegisterAppLayerDebugCallbacks((name, args) => lines.Add((name, args)));
        sub.RegisterAll();

        await Dispatch(client, GatewayConstants.Events.ConnectChallenge, new { nonce = "n1", ts = "t1" });
        await Dispatch(client, GatewayConstants.Events.Agent,
            new { stream = GatewayConstants.StreamTypes.Assistant, data = new { delta = "ab" } });
        await Dispatch(client, GatewayConstants.Events.Chat, new { state = GatewayConstants.ChatStates.Final });
        await Dispatch(client, GatewayConstants.Events.Shutdown, new { reason = "bye" });

        Assert.Contains(lines, x => x.Name == nameof(GatewayEventSubscriber.ConnectChallengeReceived)
                                    && x.Args.Contains("n1", StringComparison.Ordinal));
        Assert.Contains(lines, x => x.Name == nameof(GatewayEventSubscriber.FirstDeltaReceived));
        Assert.Contains(lines, x => x.Name == nameof(GatewayEventSubscriber.AgentDeltaReceived)
                                    && x.Args.Contains("length=2", StringComparison.Ordinal));
        Assert.Contains(lines, x => x.Name == nameof(GatewayEventSubscriber.ChatReceived));
        Assert.Contains(lines, x => x.Name == nameof(GatewayEventSubscriber.ChatTurnCompleted));
        Assert.Contains(lines, x => x.Name == nameof(GatewayEventSubscriber.ShutdownReceived)
                                    && x.Args.Contains("bye", StringComparison.Ordinal));
    }

    /// <summary>
    /// 无自定义 emit 时使用 <see cref="OpenClaw.Core.Logging.Log"/> 分支不应抛异常。
    /// </summary>
    [Fact]
    public void RegisterAppLayerDebugCallbacks_default_emit_does_not_throw()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        var sub = new GatewayEventSubscriber(client);
        var ex = Record.Exception(() => sub.RegisterAppLayerDebugCallbacks());
        Assert.Null(ex);
    }

    private static async Task Dispatch(GatewayClient client, string name, object payload)
    {
        var el = JsonSerializer.SerializeToElement(payload, JsonDefaults.SerializerOptions);
        await client.Events.DispatchAsync(new GatewayEvent { Event = name, Payload = el });
    }
}