using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Core.SignalR;
using Xunit;

namespace OpenClaw.Tests.SignalR;

public sealed class OpenClawSignalROperationApiIntegrationTests
{
    private const string OperationsBase = "api/test/openclaw/signalr/operations";

    [Fact]
    public async Task Get_connections_count_matches_in_process_service()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        using var http = CreateHttpClient(host.BaseUri);
        var hub = await ConnectAuthorizedAsync(host.BaseUri, TestJwtTokens.CreateToken("http-count-user", "guest"));
        try
        {
            var count = await http.GetFromJsonAsync<int>($"{OperationsBase}/connections/count");
            Assert.Equal(1, count);

            var svc = host.App.Services.GetRequiredService<IOpenClawSignalROperationService<OpenClawGatewayHub>>();
            Assert.Equal(count, await svc.GetOnlineConnectionCountAsync());

            await hub.StopAsync();
        }
        finally
        {
            await hub.DisposeAsync();
        }

        await SignalRTestPresencePoll.AssertHttpConnectionCountEventuallyAsync(
            http,
            $"{OperationsBase}/connections/count",
            0);
    }

    [Fact]
    public async Task Post_send_connection_delivers_json_args_to_client()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var token = TestJwtTokens.CreateToken("http-send-user", "guest");
        var hub = await ConnectAuthorizedAsync(host.BaseUri, token);
        try
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            hub.On<string>("opNotify", msg => tcs.TrySetResult(msg));

            var ops = host.App.Services.GetRequiredService<IOpenClawSignalROperationService<OpenClawGatewayHub>>();
            var connectionId = (await ops.GetOnlineConnectionsAsync())[0].ConnectionId;

            using var http = CreateHttpClient(host.BaseUri);
            var response = await http.PostAsJsonAsync(
                $"{OperationsBase}/send/connection",
                new SendConnectionApiBody(connectionId, "opNotify", new[] { "from-rest-api" }));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var msg = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("from-rest-api", msg);
        }
        finally
        {
            await hub.StopAsync();
            await hub.DisposeAsync();
        }
    }

    [Fact]
    public async Task Get_connections_me_with_bearer_returns_current_user_snapshot()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var token = TestJwtTokens.CreateToken("me-api-user", "guest");
        var hub = await ConnectAuthorizedAsync(host.BaseUri, token);
        try
        {
            using var http = CreateHttpClient(host.BaseUri, token);
            var fromMe = await http.GetFromJsonAsync<List<OpenClawSignalRConnectionSnapshot>>($"{OperationsBase}/connections/me");
            Assert.NotNull(fromMe);
            Assert.Single(fromMe!);
            Assert.Equal("me-api-user", fromMe[0].UserId);
            Assert.NotNull(fromMe[0].Principal);
        }
        finally
        {
            await hub.StopAsync();
            await hub.DisposeAsync();
        }
    }

    [Fact]
    public async Task Post_send_me_delivers_json_args_to_client()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var token = TestJwtTokens.CreateToken("send-me-user", "guest");
        var hub = await ConnectAuthorizedAsync(host.BaseUri, token);
        try
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            hub.On<string>("opNotify", msg => tcs.TrySetResult(msg));

            using var http = CreateHttpClient(host.BaseUri, token);
            var response = await http.PostAsJsonAsync(
                $"{OperationsBase}/send/me",
                new OpenClawSignalRSendToCurrentUserRequest(
                    "opNotify",
                    [JsonSerializer.SerializeToElement("from-send-me")]));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("from-send-me", await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        }
        finally
        {
            await hub.StopAsync();
            await hub.DisposeAsync();
        }
    }

    private static HttpClient CreateHttpClient(Uri baseUri, string? bearerToken = null)
    {
        var http = new HttpClient { BaseAddress = baseUri };
        if (!string.IsNullOrEmpty(bearerToken))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return http;
    }

    private static async Task<HubConnection> ConnectAuthorizedAsync(Uri baseUri, string jwt)
    {
        var hubUrl = new Uri(baseUri, $"/hubs/openclaw?access_token={Uri.EscapeDataString(jwt)}");
        var connection = new HubConnectionBuilder().WithUrl(hubUrl).Build();
        await connection.StartAsync();
        return connection;
    }

    [Fact]
    public async Task Get_distinct_users_via_http_matches_service()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var hub = await ConnectAuthorizedAsync(host.BaseUri, TestJwtTokens.CreateToken("distinct-api-user", "guest"));
        try
        {
            using var http = CreateHttpClient(host.BaseUri);
            var fromHttp = await http.GetFromJsonAsync<List<string>>($"{OperationsBase}/users/distinct");
            Assert.NotNull(fromHttp);
            Assert.Single(fromHttp!);
            Assert.Equal("distinct-api-user", fromHttp[0]);

            var svc = host.App.Services.GetRequiredService<IOpenClawSignalROperationService<OpenClawGatewayHub>>();
            var fromSvc = await svc.GetDistinctOnlineUserIdsAsync();
            Assert.Equal(fromHttp, fromSvc);
        }
        finally
        {
            await hub.StopAsync();
            await hub.DisposeAsync();
        }
    }

    [Fact]
    public async Task Get_formatted_group_names_via_http()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        using var http = CreateHttpClient(host.BaseUri);
        var userGroup = await http.GetFromJsonAsync<OpenClawSignalRFormattedGroupNameResponse>(
            $"{OperationsBase}/groups/formatted/user/my-user");
        Assert.NotNull(userGroup);
        var svc = host.App.Services.GetRequiredService<IOpenClawSignalROperationService<OpenClawGatewayHub>>();
        Assert.Equal(svc.FormatUserGroupName("my-user"), userGroup!.GroupName);

        var tierGroup = await http.GetFromJsonAsync<OpenClawSignalRFormattedGroupNameResponse>(
            $"{OperationsBase}/groups/formatted/tier/enterprise");
        Assert.Equal(svc.FormatTierGroupName("enterprise"), tierGroup!.GroupName);
    }

    [Fact]
    public async Task Post_send_user_delivers_to_client()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var token = TestJwtTokens.CreateToken("rest-send-user", "guest");
        var hub = await ConnectAuthorizedAsync(host.BaseUri, token);
        try
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            hub.On<string>("opNotify", msg => tcs.TrySetResult(msg));

            using var http = CreateHttpClient(host.BaseUri);
            var response = await http.PostAsJsonAsync(
                $"{OperationsBase}/send/user",
                new SendUserApiBody("rest-send-user", "opNotify", new[] { "from-send-user-api" }));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("from-send-user-api", await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        }
        finally
        {
            await hub.StopAsync();
            await hub.DisposeAsync();
        }
    }

    [Fact]
    public async Task Post_send_group_delivers_to_client()
    {
        await using var host = await JwtSignalRTestHost.StartAsync();

        var token = TestJwtTokens.CreateToken("rest-group-user", "guest");
        var hub = await ConnectAuthorizedAsync(host.BaseUri, token);
        try
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            hub.On<string>("opNotify", msg => tcs.TrySetResult(msg));

            var svc = host.App.Services.GetRequiredService<IOpenClawSignalROperationService<OpenClawGatewayHub>>();
            var groupName = svc.FormatUserGroupName("rest-group-user");

            using var http = CreateHttpClient(host.BaseUri);
            var response = await http.PostAsJsonAsync(
                $"{OperationsBase}/send/group",
                new SendGroupApiBody(groupName, "opNotify", new[] { "from-send-group-api" }));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("from-send-group-api", await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        }
        finally
        {
            await hub.StopAsync();
            await hub.DisposeAsync();
        }
    }

    private sealed record SendConnectionApiBody(string ConnectionId, string HubMethod, string[]? Args);

    private sealed record SendUserApiBody(string UserId, string HubMethod, string[]? Args);

    private sealed record SendGroupApiBody(string GroupName, string HubMethod, string[]? Args);
}