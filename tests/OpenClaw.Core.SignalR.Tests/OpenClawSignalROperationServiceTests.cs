using Microsoft.AspNetCore.SignalR.Client;
using OpenClaw.Core.SignalR;
using Xunit;

namespace OpenClaw.Tests.SignalR;

public sealed class OpenClawSignalROperationServiceTests
{
    [Fact]
    public async Task Presence_tracks_authenticated_user_groups_and_clears_on_disconnect()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var token = TestJwtTokens.CreateToken("ops-user", "paid");
        var connection = await ConnectAuthorizedAsync(host.BaseUri, token);
        try
        {
            var ops = host.App.Services.GetRequiredService<IOpenClawSignalROperationService<OpenClawGatewayHub>>();
            await SignalRTestPresencePoll.AssertOnlineCountEventuallyAsync(ops, 1);

            var list = await SignalRTestPresencePoll.WaitForSnapshotsNonEmptyAsync(ops);
            Assert.Single(list);
            var snap = list[0];
            Assert.Equal("ops-user", snap.UserId);
            Assert.Equal("paid", snap.Tier);
            Assert.Contains("oc:system", snap.SignalRGroups);
            Assert.NotNull(snap.Principal);
            Assert.True(snap.Principal!.IsAuthenticated);

            var byUser = await ops.GetConnectionsForUserAsync("ops-user");
            Assert.Single(byUser);

            var distinct = await ops.GetDistinctOnlineUserIdsAsync();
            Assert.Single(distinct);
            Assert.Equal("ops-user", distinct[0]);

            var counts = await ops.GetSignalRGroupConnectionCountsAsync();
            Assert.True(counts.Count > 0);
            Assert.True(counts[ops.FormatUserGroupName("ops-user")] >= 1);

            await connection.StopAsync();
        }
        finally
        {
            await connection.DisposeAsync();
        }

        var opsAfter = host.App.Services.GetRequiredService<IOpenClawSignalROperationService<OpenClawGatewayHub>>();
        await SignalRTestPresencePoll.AssertOnlineCountEventuallyAsync(opsAfter, 0);
    }

    [Fact]
    public async Task SendToConnectionAsync_delivers_custom_client_method()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var token = TestJwtTokens.CreateToken("recv-user", "guest");
        await using var connection = await ConnectAuthorizedAsync(host.BaseUri, token);

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<string>("opNotify", msg => tcs.TrySetResult(msg));

        var ops = host.App.Services.GetRequiredService<IOpenClawSignalROperationService<OpenClawGatewayHub>>();
        var connId = (await SignalRTestPresencePoll.WaitForSnapshotsNonEmptyAsync(ops))[0].ConnectionId;
        await ops.SendToConnectionAsync(connId, "opNotify", ["hello-ops"]);

        var msg = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("hello-ops", msg);
    }

    [Fact]
    public async Task SendToUserAsync_delivers_to_all_connections_of_user()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var token = TestJwtTokens.CreateToken("multi-user", "guest");
        await using var c1 = await ConnectAuthorizedAsync(host.BaseUri, token);
        await using var c2 = await ConnectAuthorizedAsync(host.BaseUri, token);

        var tcs1 = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcs2 = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        c1.On<string>("opNotify", msg => tcs1.TrySetResult(msg));
        c2.On<string>("opNotify", msg => tcs2.TrySetResult(msg));

        var ops = host.App.Services.GetRequiredService<IOpenClawSignalROperationService<OpenClawGatewayHub>>();
        await SignalRTestPresencePoll.AssertOnlineCountEventuallyAsync(ops, 2);
        await ops.SendToUserAsync("multi-user", "opNotify", ["broadcast"]);

        Assert.Equal("broadcast", await tcs1.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal("broadcast", await tcs2.Task.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task Anonymous_hub_connection_has_null_user_and_optional_groups_only()
    {
        await using var host = await SignalRTestHost.StartAsync(registerFakeRpc: true);

        await using var connection = await ConnectAnonymousAsync(host.BaseUri);

        var ops = host.App.Services.GetRequiredService<IOpenClawSignalROperationService<OpenClawGatewayHubAllowAnonymous>>();
        await SignalRTestPresencePoll.AssertOnlineCountEventuallyAsync(ops, 1);
        var snap = (await SignalRTestPresencePoll.WaitForSnapshotsNonEmptyAsync(ops))[0];
        Assert.Null(snap.UserId);
        Assert.NotNull(snap.Principal);
        Assert.False(snap.Principal!.IsAuthenticated);
        Assert.Empty(await ops.GetDistinctOnlineUserIdsAsync());

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Hybrid_presence_tracks_connection_and_groups()
    {
        await using var host = await OpenClawSignalRHybridPresenceTestHost.StartAsync();

        var token = TestJwtTokens.CreateToken("hybrid-user", "paid");
        var ops = host.App.Services.GetRequiredService<IOpenClawSignalROperationService<OpenClawGatewayHub>>();
        {
            await using var connection = await ConnectAuthorizedAsync(host.BaseUri, token);
            var list = await SignalRTestPresencePoll.WaitForSnapshotsNonEmptyAsync(ops);
            Assert.Single(list);
            Assert.Equal("hybrid-user", list[0].UserId);
            Assert.Contains("oc:system", list[0].SignalRGroups);
        }

        await SignalRTestPresencePoll.AssertOnlineCountEventuallyAsync(ops, 0);
    }

    [Fact]
    public async Task SendToGroupAsync_delivers_to_connections_in_user_group()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var token = TestJwtTokens.CreateToken("group-target", "guest");
        await using var connection = await ConnectAuthorizedAsync(host.BaseUri, token);

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<string>("opNotify", msg => tcs.TrySetResult(msg));

        var ops = host.App.Services.GetRequiredService<IOpenClawSignalROperationService<OpenClawGatewayHub>>();
        await SignalRTestPresencePoll.AssertOnlineCountEventuallyAsync(ops, 1);
        var group = ops.FormatUserGroupName("group-target");
        await ops.SendToGroupAsync(group, "opNotify", ["via-group"]);

        var msg = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("via-group", msg);
    }

    private static async Task<HubConnection> ConnectAuthorizedAsync(Uri baseUri, string jwt)
    {
        var hubUrl = new Uri(baseUri, $"/hubs/openclaw?access_token={Uri.EscapeDataString(jwt)}");
        var connection = new HubConnectionBuilder().WithUrl(hubUrl).Build();
        await connection.StartAsync();
        return connection;
    }

    private static async Task<HubConnection> ConnectAnonymousAsync(Uri baseUri)
    {
        var hubUrl = new Uri(baseUri, "/hubs/openclaw");
        var connection = new HubConnectionBuilder().WithUrl(hubUrl).Build();
        await connection.StartAsync();
        return connection;
    }
}
