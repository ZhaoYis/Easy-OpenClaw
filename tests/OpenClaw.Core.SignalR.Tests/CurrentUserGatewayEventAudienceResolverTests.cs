using Microsoft.AspNetCore.SignalR;
using OpenClaw.Core.Models;
using OpenClaw.Core.SignalR;
using Xunit;

namespace OpenClaw.Tests.SignalR;

public sealed class CurrentUserGatewayEventAudienceResolverTests
{
    private static readonly OpenClawSignalROptions Options = new();

    [Fact]
    public void TryResolveClients_wrong_state_returns_false()
    {
        var resolver = new CurrentUserGatewayEventAudienceResolver();
        var clients = new CapturingHubClients();
        var ctx = new GatewayEventAudienceResolveContext(
            new GatewayEvent { Event = "tick" },
            clients,
            Options,
            null,
            new object());

        Assert.False(resolver.TryResolveClients(ctx, out var target));
        Assert.Null(target);
        Assert.Null(clients.CapturedUserId);
    }

    [Fact]
    public void TryResolveClients_null_user_id_returns_false()
    {
        var resolver = new CurrentUserGatewayEventAudienceResolver();
        var clients = new CapturingHubClients();
        var ctx = new GatewayEventAudienceResolveContext(
            new GatewayEvent { Event = "tick" },
            clients,
            Options,
            null,
            new OpenClawSignalRGatewayHubBridgeContext("cid", null));

        Assert.False(resolver.TryResolveClients(ctx, out var target));
        Assert.Null(target);
        Assert.Null(clients.CapturedUserId);
    }

    [Fact]
    public void TryResolveClients_uses_bridge_user_id()
    {
        var resolver = new CurrentUserGatewayEventAudienceResolver();
        var clients = new CapturingHubClients();
        var ctx = new GatewayEventAudienceResolveContext(
            new GatewayEvent { Event = "tick" },
            clients,
            Options,
            null,
            new OpenClawSignalRGatewayHubBridgeContext("cid", "user-42"));

        Assert.True(resolver.TryResolveClients(ctx, out var target));
        Assert.NotNull(target);
        Assert.Same(CapturingHubClients.DummyProxy, target);
        Assert.Equal("user-42", clients.CapturedUserId);
    }

    private sealed class CapturingHubClients : IHubClients
    {
        internal static readonly IClientProxy DummyProxy = new NoOpClientProxy();

        public string? CapturedUserId { get; private set; }

        public IClientProxy All => throw new NotImplementedException();

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();

        public IClientProxy Client(string connectionId) => throw new NotImplementedException();

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();

        public IClientProxy Group(string groupName) => throw new NotImplementedException();

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) =>
            throw new NotImplementedException();

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotImplementedException();

        public IClientProxy User(string userId)
        {
            CapturedUserId = userId;
            return DummyProxy;
        }

        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
    }

    private sealed class NoOpClientProxy : IClientProxy
    {
        public Task SendAsync(string method, object? arg1, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendAsync(string method, object? arg1, object? arg2, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendAsync(string method, object? arg1, object? arg2, object? arg3,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendAsync(string method, object? arg1, object? arg2, object? arg3, object? arg4,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendAsync(string method, object? arg1, object? arg2, object? arg3, object? arg4, object? arg5,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendAsync(string method, object? arg1, object? arg2, object? arg3, object? arg4, object? arg5,
            object? arg6, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendAsync(string method, object? arg1, object? arg2, object? arg3, object? arg4, object? arg5,
            object? arg6, object? arg7, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendAsync(string method, object? arg1, object? arg2, object? arg3, object? arg4, object? arg5,
            object? arg6, object? arg7, object? arg8, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
