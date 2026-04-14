using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenClaw.Core.SignalR;
using Xunit;

namespace OpenClaw.Tests.SignalR;

public sealed class OpenClawSignalRDistributedConnectionIdIndexTests
{
    private static (OpenClawSignalRDistributedConnectionIdIndex Index, IDistributedCache Cache) CreateIndex()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        var sp = services.BuildServiceProvider();
        var cache = sp.GetRequiredService<IDistributedCache>();
        var options = Options.Create(new OpenClawSignalROptions
        {
            ConnectionPresenceIndexKey = "test:index:" + Guid.NewGuid().ToString("N"),
        });
        return (new OpenClawSignalRDistributedConnectionIdIndex(cache, options), cache);
    }

    [Fact]
    public async Task GetAllIndexTokensAsync_empty_when_no_data()
    {
        var (index, _) = CreateIndex();
        var tokens = await index.GetAllIndexTokensAsync();
        Assert.Empty(tokens);
    }

    [Fact]
    public async Task AddAsync_and_GetAllIndexTokensAsync_roundtrip()
    {
        var (index, _) = CreateIndex();
        await index.AddAsync("user|c1");
        await index.AddAsync("user|c2");
        var tokens = await index.GetAllIndexTokensAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Contains("user|c1", tokens);
        Assert.Contains("user|c2", tokens);
    }

    [Fact]
    public async Task AddAsync_duplicate_is_idempotent()
    {
        var (index, _) = CreateIndex();
        await index.AddAsync("user|c1");
        await index.AddAsync("user|c1");
        var tokens = await index.GetAllIndexTokensAsync();
        Assert.Single(tokens);
        Assert.Equal("user|c1", tokens[0]);
    }

    [Fact]
    public async Task RemoveAsync_last_entry_yields_empty_list()
    {
        var (index, _) = CreateIndex();
        await index.AddAsync("u|only");
        await index.RemoveAsync("u|only");
        var tokens = await index.GetAllIndexTokensAsync();
        Assert.Empty(tokens);
    }

    [Fact]
    public async Task RemoveAsync_missing_token_is_noop()
    {
        var (index, _) = CreateIndex();
        await index.AddAsync("u|a");
        await index.RemoveAsync("u|ghost");
        var tokens = await index.GetAllIndexTokensAsync();
        Assert.Single(tokens);
    }

    [Fact]
    public async Task RemoveAsync_one_of_many_leaves_others()
    {
        var (index, _) = CreateIndex();
        await index.AddAsync("u|a");
        await index.AddAsync("u|b");
        await index.RemoveAsync("u|a");
        var tokens = await index.GetAllIndexTokensAsync();
        Assert.Single(tokens);
        Assert.Equal("u|b", tokens[0]);
    }
}
