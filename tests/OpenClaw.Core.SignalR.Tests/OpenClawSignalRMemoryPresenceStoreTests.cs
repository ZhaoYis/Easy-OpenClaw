using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenClaw.Core.SignalR;
using Xunit;

namespace OpenClaw.Tests.SignalR;

public sealed class OpenClawSignalRMemoryPresenceStoreTests
{
    [Fact]
    public async Task Register_and_GetSnapshots_roundtrip_anonymous_user_key_segment()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton(Options.Create(new OpenClawSignalROptions()));
        services.AddSingleton<IOpenClawSignalRConnectionPresenceStore, OpenClawSignalRMemoryConnectionPresenceStore>();
        await using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IOpenClawSignalRConnectionPresenceStore>();
        var snap = new OpenClawSignalRConnectionSnapshot(
            "test-conn-id",
            null,
            null,
            DateTimeOffset.UtcNow,
            [],
            OpenClawSignalRPrincipalSnapshot.From(null));

        await store.RegisterAsync(snap);
        var list = await store.GetSnapshotsAsync();
        Assert.Single(list);
        Assert.Equal("test-conn-id", list[0].ConnectionId);
    }
}
