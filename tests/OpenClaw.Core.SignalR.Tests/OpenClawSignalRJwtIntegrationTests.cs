using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using OpenClaw.Core.Client;
using OpenClaw.Core.Models;
using OpenClaw.Core.SignalR;
using Xunit;

namespace OpenClaw.Tests.SignalR;

public sealed class OpenClawSignalRJwtIntegrationTests
{
    [Fact]
    public async Task Hub_rejects_connection_without_jwt()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var hubUrl = new Uri(host.BaseUri, "/hubs/openclaw");
        await using var connection = new HubConnectionBuilder().WithUrl(hubUrl).Build();

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task Hub_accepts_connection_with_query_access_token()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var token = TestJwtTokens.CreateToken("user-1", "paid");
        await using var connection = await ConnectAuthorizedAsync(host.BaseUri, token);

        var state = await connection.InvokeAsync<OpenClawGatewayStateDto>("getGatewayStateAsync");
        Assert.True(state.IsConnected);
    }

    [Fact]
    public async Task Gateway_event_routed_only_to_payload_target_user()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var tokenA = TestJwtTokens.CreateToken("user-a", "paid");
        var tokenB = TestJwtTokens.CreateToken("user-b", "paid");

        await using var connA = await ConnectAuthorizedAsync(host.BaseUri, tokenA);
        await using var connB = await ConnectAuthorizedAsync(host.BaseUri, tokenB);

        var tcsA = new TaskCompletionSource<GatewayEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcsB = new TaskCompletionSource<GatewayEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connA.On<GatewayEvent>("GatewayEvent", e => tcsA.TrySetResult(e));
        connB.On<GatewayEvent>("GatewayEvent", e => tcsB.TrySetResult(e));

        var gateway = host.App.Services.GetRequiredService<GatewayClient>();
        var evt = new GatewayEvent
        {
            Event = "agent",
            Payload = JsonSerializer.SerializeToElement(new { targetUserId = "user-a", delta = "x" }),
        };
        await gateway.Events.DispatchAsync(evt);

        var receivedA = await tcsA.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("agent", receivedA.Event);

        await Task.Delay(500);
        Assert.False(tcsB.Task.IsCompleted);
    }

    [Fact]
    public async Task System_broadcast_reaches_all_authenticated_connections()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var tokenA = TestJwtTokens.CreateToken("user-a", "guest");
        var tokenB = TestJwtTokens.CreateToken("user-b", "enterprise");

        await using var connA = await ConnectAuthorizedAsync(host.BaseUri, tokenA);

        var ops = host.App.Services.GetRequiredService<IOpenClawSignalROperationService<OpenClawGatewayHub>>();
        await SignalRTestPresencePoll.AssertOnlineCountEventuallyAsync(ops, 1);

        await using var connB = await ConnectAuthorizedAsync(host.BaseUri, tokenB);
        await SignalRTestPresencePoll.AssertOnlineCountEventuallyAsync(ops, 2);

        var snapshots = await ops.GetOnlineConnectionsAsync();
        Assert.Equal(2, snapshots.Count);
        Assert.All(snapshots, s => Assert.Contains("oc:system", s.SignalRGroups));

        // 勿用 On<object>：System.Text.Json 对匿名对象载荷往往无法反序列化为 object，回调不会触发导致超时。
        var tcsA = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcsB = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connA.On<JsonElement>("systemBroadcast", el =>
        {
            tcsA.TrySetResult(el);
            return Task.CompletedTask;
        });
        connB.On<JsonElement>("systemBroadcast", el =>
        {
            tcsB.TrySetResult(el);
            return Task.CompletedTask;
        });

        var sender = host.App.Services.GetRequiredService<IOpenClawSystemBroadcastSender<OpenClawGatewayHub>>();
        await sender.SendAsync(new { kind = "maintenance", message = "hello" });

        var payloadA = await tcsA.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var payloadB = await tcsB.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("maintenance", payloadA.GetProperty("kind").GetString());
        Assert.Equal("hello", payloadA.GetProperty("message").GetString());
        Assert.Equal("maintenance", payloadB.GetProperty("kind").GetString());
        Assert.Equal("hello", payloadB.GetProperty("message").GetString());
    }

    private static async Task<HubConnection> ConnectAuthorizedAsync(Uri baseUri, string jwt)
    {
        var hubUrl = new Uri(baseUri, $"/hubs/openclaw?access_token={Uri.EscapeDataString(jwt)}");
        var connection = new HubConnectionBuilder().WithUrl(hubUrl).Build();
        await connection.StartAsync();
        return connection;
    }
}
