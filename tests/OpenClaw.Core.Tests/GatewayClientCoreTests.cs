using System.Text.Json;
using OpenClaw.Core.Client;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Models;
using OpenClaw.Core.Tests.Support;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="GatewayClient"/> 核心行为测试：握手解析、RPC 跳过策略、入站帧管线、设备令牌与私有辅助方法反射覆盖。
/// </summary>
public sealed class GatewayClientCoreTests
{
    /// <summary>
    /// 未握手时 <see cref="GatewayClient.AvailableMethods"/> / <see cref="GatewayClient.AvailableEvents"/> 应为空集；
    /// <see cref="GatewayClient.IsRpcMethodAdvertised"/> 在列表缺失时应返回 null。
    /// </summary>
    [Fact]
    public void Advertised_methods_null_when_no_hello_ok()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        Assert.Empty(client.AvailableMethods);
        Assert.Empty(client.AvailableEvents);
        Assert.Null(client.IsRpcMethodAdvertised("any"));
    }

    /// <summary>
    /// <see cref="NotPairedException"/> 应保留错误详情 JSON。
    /// </summary>
    [Fact]
    public void NotPairedException_stores_error_detail()
    {
        var err = JsonSerializer.SerializeToElement(new { code = GatewayConstants.ErrorCodes.NotPaired }, JsonDefaults.SerializerOptions);
        var ex = new NotPairedException("x", err);
        Assert.Contains(GatewayConstants.ErrorCodes.NotPaired, ex.ErrorDetail?.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 处理完整 hello-ok 后应填充 <see cref="GatewayClient.HelloOk"/> 与能力列表，并在提供 DeviceToken 时写入磁盘。
    /// </summary>
    [Fact]
    public async Task ProcessHelloOk_persists_device_token_and_sets_features()
    {
        var tokenPath = Path.Combine(Path.GetTempPath(), $"openclaw-dt-{Guid.NewGuid():N}.txt");
        try
        {
            var (client, ws) = GatewayClientTestFactory.CreateWithSocket(
                c => new LoopbackWebSocket(c),
                o => o.DeviceTokenFilePath = tokenPath);

            var hello = new HelloOkPayload
            {
                Type = "hello-ok",
                Protocol = 2,
                Server = new ServerInfo { Version = "1", ConnId = "connection-id-12345678" },
                Features = new FeaturesInfo
                {
                    Methods = ["health", GatewayConstants.Methods.SessionsSubscribe],
                    Events = ["tick"],
                },
                Policy = new PolicyInfo { MaxPayload = 10_485_760, MaxBufferedBytes = 1, TickIntervalMs = 1000 },
                CanvasHostUrl = "https://canvas",
                Auth = new HelloAuthInfo
                {
                    Role = "operator",
                    Scopes = ["operator.admin"],
                    DeviceToken = "stored-token",
                },
                Snapshot = new SnapshotInfo
                {
                    UptimeMs = 86_400_000,
                    AuthMode = "token",
                    UpdateAvailable = new UpdateAvailableInfo
                    {
                        CurrentVersion = "1",
                        LatestVersion = "2",
                        Channel = "stable",
                    },
                    SessionDefaults = new SessionDefaultsInfo
                    {
                        DefaultAgentId = "a",
                        MainKey = "m",
                        MainSessionKey = "agent:custom:main",
                        Scope = "agent",
                    },
                    Presence = [new PresenceEntry { DeviceId = "d1" }],
                    Health = new HealthInfo
                    {
                        Ok = true,
                        Ts = 1,
                        DurationMs = 2,
                        Channels = new Dictionary<string, ChannelHealth>
                        {
                            ["tg"] = new ChannelHealth { Configured = true, Running = true },
                        },
                        ChannelLabels = new Dictionary<string, string> { ["tg"] = "Telegram" },
                        Agents = [new AgentHealth { AgentId = "ag", IsDefault = true }],
                        Sessions = new SessionsSummary { Count = 3 },
                    },
                    StateVersion = new StateVersionInfo { Presence = 1, Health = 2 },
                    ConfigPath = "/cfg",
                },
            };

            var resp = GatewayClientPrivateApi.ResponseWithHelloOk(hello);
            GatewayClientPrivateApi.ProcessHelloOk(client, resp);

            Assert.NotNull(client.HelloOk);
            Assert.Contains("health", client.AvailableMethods);
            Assert.True(client.IsRpcMethodAdvertised("health"));
            Assert.False(client.IsRpcMethodAdvertised("missing.method"));
            Assert.Equal("stored-token", File.ReadAllText(tokenPath).Trim());

            var r = await client.ChatSendAsync("hi");
            Assert.True(r.Ok);
            Assert.Contains("agent:custom:main", ws.SentPayloads[^1]);
        }
        finally
        {
            try
            {
                File.Delete(tokenPath);
            }
            catch
            {
                // best-effort
            }
        }
    }

    /// <summary>
    /// 上一测试已验证 ChatSend 使用主会话键；本用例单独断言出站 JSON 含默认会话（无 hello 时）。
    /// </summary>
    [Fact]
    public async Task ChatSend_uses_default_session_when_no_snapshot()
    {
        var (client, ws) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        var r = await client.ChatSendAsync("m");
        Assert.True(r.Ok);
        Assert.Contains(GatewayConstants.DefaultSessionKey, ws.SentPayloads[^1]);
    }

    /// <summary>
    /// <see cref="GatewayClient.SessionsSubscribeAsync"/> 在 hello-ok 声明不含该方法时应返回本地 skipped 响应。
    /// </summary>
    [Fact]
    public async Task SessionsSubscribe_skipped_when_not_advertised()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        var hello = new HelloOkPayload
        {
            Type = "hello-ok",
            Protocol = 1,
            Features = new FeaturesInfo { Methods = ["health"], Events = [] },
        };
        GatewayClientPrivateApi.ProcessHelloOk(client, GatewayClientPrivateApi.ResponseWithHelloOk(hello));
        var r = await client.SessionsSubscribeAsync();
        Assert.True(r.Ok);
        Assert.Contains("skipped", r.Payload?.GetRawText() ?? "", StringComparison.Ordinal);
    }

    /// <summary>
    /// <see cref="GatewayClient.SessionsMessagesSubscribeAsync(OpenClaw.Core.Models.SessionsMessagesKeyParams, CancellationToken)"/> 未声明时同样跳过。
    /// </summary>
    [Fact]
    public async Task SessionsMessagesSubscribe_skipped_when_not_advertised()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        var hello = new HelloOkPayload
        {
            Type = "hello-ok",
            Protocol = 1,
            Features = new FeaturesInfo { Methods = ["health"], Events = [] },
        };
        GatewayClientPrivateApi.ProcessHelloOk(client, GatewayClientPrivateApi.ResponseWithHelloOk(hello));
        var r = await client.SessionsMessagesSubscribeAsync(GatewayConstants.DefaultSessionKey);
        Assert.True(r.Ok);
        Assert.Contains("not_advertised", r.Payload?.GetRawText() ?? "", StringComparison.Ordinal);
    }

    /// <summary>
    /// 反射覆盖静态错误识别与 JSON 取字段辅助方法。
    /// </summary>
    [Fact]
    public void Private_static_helpers_detect_not_paired_and_read_json()
    {
        var el = JsonSerializer.SerializeToElement(new { nonce = "n1" }, JsonDefaults.SerializerOptions);
        Assert.Equal("n1", GatewayClientPrivateApi.GetString(el, "nonce"));
        Assert.Equal("", GatewayClientPrivateApi.GetString(null, "x"));
        var notPaired = JsonSerializer.SerializeToElement(new { x = GatewayConstants.ErrorCodes.NotPaired }, JsonDefaults.SerializerOptions);
        Assert.True(GatewayClientPrivateApi.IsNotPairedError(notPaired));
        Assert.False(GatewayClientPrivateApi.IsNotPairedError(null));
    }

    /// <summary>
    /// <see cref="GatewayClientPrivateApi.CalculateBackoff"/> 应受配置上下限约束。
    /// </summary>
    [Fact]
    public void CalculateBackoff_respects_options_cap()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(
            c => new LoopbackWebSocket(c),
            o =>
            {
                o.PairingRetryDelay = TimeSpan.FromSeconds(10);
                o.PairingRetryMaxDelay = TimeSpan.FromSeconds(15);
            });
        var d = GatewayClientPrivateApi.CalculateBackoff(client, 99);
        Assert.Equal(TimeSpan.FromSeconds(15), d);
    }

    /// <summary>
    /// <see cref="GatewayClientPrivateApi.BuildConnectParams"/> 应生成带签名的设备信息与客户端元数据。
    /// </summary>
    [Fact]
    public void BuildConnectParams_contains_device_and_signature()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        var p = GatewayClientPrivateApi.BuildConnectParams(client, "nonce-xyz");
        Assert.Equal("nonce-xyz", p.Device?.Nonce);
        Assert.False(string.IsNullOrEmpty(p.Device?.Signature));
        Assert.False(string.IsNullOrEmpty(p.Client?.Id));
    }

    /// <summary>
    /// 入站非法 JSON 不应抛出到测试线程。
    /// </summary>
    [Fact]
    public async Task SimulateIncoming_malformed_json_swallowed()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        await client.SimulateIncomingJsonForTests("{");
    }

    /// <summary>
    /// 未知帧类型应走 warn 分支而不抛异常。
    /// </summary>
    [Fact]
    public async Task SimulateIncoming_unknown_frame_type_warns()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        await client.SimulateIncomingJsonForTests("""{"type":"weird"}""");
    }

    /// <summary>
    /// 未匹配的 response id 应被忽略（TryComplete 返回 false）。
    /// </summary>
    [Fact]
    public async Task SimulateIncoming_orphan_response_does_not_throw()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        await client.SimulateIncomingJsonForTests(
            """{"type":"res","id":"00000000-0000-0000-0000-000000000000","ok":true,"payload":{}}""");
    }

    /// <summary>
    /// 事件帧应进入 <see cref="EventRouter"/>。
    /// </summary>
    [Fact]
    public async Task SimulateIncoming_event_dispatches_router()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        var hit = false;
        client.Events.On("tick", _ =>
        {
            hit = true;
            return Task.CompletedTask;
        });
        await client.SimulateIncomingJsonForTests("""{"type":"event","event":"tick","seq":1}""");
        Assert.True(hit);
    }

    /// <summary>
    /// <see cref="GatewayClient"/> 使用错误环回应答时 <see cref="OpenClaw.Core.Client.GatewayClient.HealthAsync"/> 应走 Invoke 失败日志分支。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_logs_error_on_failed_rpc()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new RpcErrorLoopbackWebSocket(c));
        var r = await client.HealthAsync();
        Assert.False(r.Ok);
    }

    /// <summary>
    /// 长 payload 时 Invoke 成功路径应截断日志预览（覆盖 Truncate 分支）。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_truncates_long_success_payload()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new FatPayloadLoopbackWebSocket(c));
        var r = await client.HealthAsync();
        Assert.True(r.Ok);
    }

    /// <summary>
    /// <see cref="GatewayClient.SkillsUpdateClawHubAsync"/> 的 updateAll 与 slug 分支均应产生出站请求。
    /// </summary>
    [Fact]
    public async Task SkillsUpdateClawHubAsync_branches_send_distinct_bodies()
    {
        var (client, ws) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        _ = await client.SkillsUpdateClawHubAsync(slug: "s1");
        _ = await client.SkillsUpdateClawHubAsync(updateAll: true);
        Assert.Contains("all", ws.SentPayloads[^1]);
        Assert.Contains("slug", ws.SentPayloads[^2]);
    }

    /// <summary>
    /// 泛型与 <see cref="JsonElement"/> 重载的 <c>SendRequestAsync</c> 均应完成。
    /// </summary>
    [Fact]
    public async Task SendRequestAsync_overloads_complete()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        var r1 = await client.SendRequestAsync("custom", new { a = 1 });
        Assert.True(r1.Ok);
        var r2 = await client.SendRequestAsync(
            "custom2",
            JsonSerializer.SerializeToElement(new { b = 2 }, JsonDefaults.SerializerOptions));
        Assert.True(r2.Ok);
    }

    /// <summary>
    /// <see cref="GatewayClient.OnEvent"/> 应委托给内部路由器。
    /// </summary>
    [Fact]
    public async Task OnEvent_registers_on_router()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        var ok = false;
        client.OnEvent("e", _ =>
        {
            ok = true;
            return Task.CompletedTask;
        });
        await client.Events.DispatchAsync(new GatewayEvent { Event = "e" });
        Assert.True(ok);
    }

    /// <summary>
    /// <see cref="GatewayClient.DisposeAsync"/> 应可重复调用且释放资源。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_is_idempotent()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        await client.DisposeAsync();
        await client.DisposeAsync();
    }

    /// <summary>
    /// ProcessHelloOk 在 payload 为空或反序列化失败时应安全返回（覆盖早退分支）。
    /// </summary>
    [Fact]
    public void ProcessHelloOk_no_payload_no_throw()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));
        GatewayClientPrivateApi.ProcessHelloOk(client, new GatewayResponse { Ok = true, Payload = null });
    }
}
