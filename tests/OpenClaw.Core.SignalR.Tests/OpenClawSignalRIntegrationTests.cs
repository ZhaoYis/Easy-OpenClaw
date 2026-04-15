using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using OpenClaw.Core.Client;
using OpenClaw.Core.Extensions;
using OpenClaw.Core.Models;
using OpenClaw.Core.SignalR;
using Xunit;

namespace OpenClaw.Tests.SignalR;

/// <summary>
/// SignalR Hub 与网关事件广播的集成测试（真实 Kestrel + SignalR 客户端）。
/// </summary>
public sealed class OpenClawSignalRIntegrationTests
{
    [Fact]
    public async Task GetGatewayStateAsync_returns_state_from_rpc_backend()
    {
        await using var host = await SignalRTestHost.StartAsync(registerFakeRpc: true, configureSignalR: o =>
        {
            o.AllowedRpcMethods = ["health"];
        });

        await using var connection = await ConnectHubAsync(host.BaseUri);

        var state = await connection.InvokeAsync<OpenClawGatewayStateDto>("getGatewayStateAsync");

        Assert.True(state.IsConnected);
        Assert.Equal(ConnectionState.Connected, state.State);
        Assert.Contains("health", state.AvailableMethods);
        Assert.Contains("agent", state.AvailableEvents);
    }

    [Fact]
    public async Task InvokeRpcAsync_succeeds_when_allowlisted_and_fake_connected()
    {
        await using var host = await SignalRTestHost.StartAsync(registerFakeRpc: true, configureSignalR: o =>
        {
            o.AllowedRpcMethods = ["health"];
        });

        await using var connection = await ConnectHubAsync(host.BaseUri);

        var resp = await connection.InvokeAsync<GatewayResponse>("invokeRpcAsync", "health", null);

        Assert.True(resp.Ok);
        Assert.NotNull(resp.Payload);
    }

    [Fact]
    public async Task InvokeRpcAsync_throws_when_method_not_in_allowlist()
    {
        await using var host = await SignalRTestHost.StartAsync(registerFakeRpc: true, configureSignalR: o =>
        {
            o.AllowedRpcMethods = ["health"];
        });

        await using var connection = await ConnectHubAsync(host.BaseUri);

        var ex = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() =>
            connection.InvokeAsync<GatewayResponse>("invokeRpcAsync", "chat.send", null));

        Assert.True(
            ex.Message.Contains("not allowed", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("RPC method not allowed", StringComparison.OrdinalIgnoreCase),
            ex.Message);
    }

    [Fact]
    public async Task InvokeRpcAsync_throws_when_gateway_not_connected()
    {
        await using var host2 = await DisconnectedRpcHost.StartAsync();

        await using var connection = await ConnectHubAsync(host2.BaseUri);

        var ex = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() =>
            connection.InvokeAsync<GatewayResponse>("invokeRpcAsync", "health", null));

        Assert.True(
            ex.Message.Contains("not connected", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Gateway is not connected", StringComparison.OrdinalIgnoreCase),
            ex.Message);
    }

    [Fact]
    public async Task Broadcaster_forwards_dispatched_event_to_signalr_client()
    {
        await using var host = await SignalRTestHost.StartAsync(registerFakeRpc: true);

        var tcs = new TaskCompletionSource<GatewayEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = await ConnectHubAsync(host.BaseUri);
        connection.On<GatewayEvent>("GatewayEvent", evt => tcs.TrySetResult(evt));

        var client = host.App.Services.GetRequiredService<GatewayClient>();
        var evt = new GatewayEvent
        {
            Event = "agent",
            Payload = JsonSerializer.SerializeToElement(new { delta = "hi" }),
        };
        await client.Events.DispatchAsync(evt);

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("agent", received.Event);
        Assert.True(received.Payload?.TryGetProperty("delta", out var d) == true && d.GetString() == "hi");
    }

    [Fact]
    public async Task Broadcaster_respects_event_allowlist()
    {
        await using var host = await SignalRTestHost.StartAsync(registerFakeRpc: true, configureSignalR: o =>
        {
            o.EventAllowlist = ["agent"];
        });

        var received = new List<string>();
        await using var connection = await ConnectHubAsync(host.BaseUri);
        connection.On<GatewayEvent>("GatewayEvent", e => received.Add(e.Event));

        var gatewayClient = host.App.Services.GetRequiredService<GatewayClient>();
        await gatewayClient.Events.DispatchAsync(new GatewayEvent { Event = "tick" });
        await Task.Delay(300);
        await gatewayClient.Events.DispatchAsync(new GatewayEvent { Event = "agent" });
        await Task.Delay(500);

        Assert.Single(received);
        Assert.Equal("agent", received[0]);
    }

    [Fact]
    public async Task Broadcaster_skips_when_GatewayEventBroadcastMode_None()
    {
        await using var host = await SignalRTestHost.StartAsync(registerFakeRpc: true, configureSignalR: o =>
        {
            o.GatewayEventBroadcastMode = GatewayEventBroadcastMode.None;
        });

        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = await ConnectHubAsync(host.BaseUri);
        connection.On<GatewayEvent>("GatewayEvent", _ => received.TrySetResult(true));

        var gatewayClient = host.App.Services.GetRequiredService<GatewayClient>();
        await gatewayClient.Events.DispatchAsync(new GatewayEvent { Event = "agent" });

        await Task.Delay(500);
        Assert.False(received.Task.IsCompleted);
    }

    private static async Task<HubConnection> ConnectHubAsync(Uri baseUri)
    {
        var hubUrl = new Uri(baseUri, "/hubs/openclaw");
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .Build();

        await connection.StartAsync();
        return connection;
    }

    /// <summary>使用未连接的 <see cref="FakeOpenClawGatewayRpc"/>，避免与默认单例冲突。</summary>
    private sealed class DisconnectedRpcHost : IAsyncDisposable
    {
        private WebApplication? _app;

        public Uri BaseUri { get; private init; } = null!;

        public static async Task<DisconnectedRpcHost> StartAsync(CancellationToken cancellationToken = default)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseKestrel(static k => k.Listen(IPAddress.Loopback, 0));
            builder.Logging.ClearProviders();

            builder.Services.AddOpenClaw(o =>
            {
                o.Url = "ws://127.0.0.1:1";
                o.Token = "integration-test";
                o.KeyFilePath = null;
            });

            builder.Services.AddSignalR(o => o.EnableDetailedErrors = true);
            builder.Services.AddSingleton<IOpenClawGatewayRpc>(_ =>
                new FakeOpenClawGatewayRpc(connected: false, state: ConnectionState.Disconnected));

            builder.Services.AddSingleton<IGatewayEventAudienceResolver, AllClientsGatewayEventAudienceResolver>();

            builder.Services.AddOpenClawSignalRGateway<OpenClawGatewayHubAllowAnonymous>(configure: o =>
                    o.EnableBackgroundConnect = false)
                .UseMemoryStore();

            var app = builder.Build();
            app.MapHub<OpenClawGatewayHubAllowAnonymous>("/hubs/openclaw");
            await app.StartAsync(cancellationToken).ConfigureAwait(false);

            var baseUri = SignalRIntegrationTestHostUtilities.ResolveHttpBaseUri(app);
            return new DisconnectedRpcHost { _app = app, BaseUri = baseUri };
        }

        public async ValueTask DisposeAsync()
        {
            if (_app is null)
                return;
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
    }
}
