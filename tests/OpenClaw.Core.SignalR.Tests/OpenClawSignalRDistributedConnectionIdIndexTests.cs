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
    public async Task GetAllIdsAsync_empty_when_no_data()
    {
        var (index, _) = CreateIndex();
        var ids = await index.GetAllIdsAsync();
        Assert.Empty(ids);
    }

    [Fact]
    public async Task AddAsync_and_GetAllIdsAsync_roundtrip()
    {
        var (index, _) = CreateIndex();
        await index.AddAsync("c1");
        await index.AddAsync("c2");
        var ids = await index.GetAllIdsAsync();
        Assert.Equal(2, ids.Count);
        Assert.Contains("c1", ids);
        Assert.Contains("c2", ids);
    }

    [Fact]
    public async Task AddAsync_duplicate_is_idempotent()
    {
        var (index, _) = CreateIndex();
        await index.AddAsync("c1");
        await index.AddAsync("c1");
        var ids = await index.GetAllIdsAsync();
        Assert.Single(ids);
        Assert.Equal("c1", ids[0]);
    }

    [Fact]
    public async Task RemoveAsync_last_entry_yields_empty_list()
    {
        var (index, _) = CreateIndex();
        await index.AddAsync("only");
        await index.RemoveAsync("only");
        var ids = await index.GetAllIdsAsync();
        Assert.Empty(ids);
    }

    [Fact]
    public async Task RemoveAsync_missing_id_is_noop()
    {
        var (index, _) = CreateIndex();
        await index.AddAsync("a");
        await index.RemoveAsync("ghost");
        var ids = await index.GetAllIdsAsync();
        Assert.Single(ids);
    }

    [Fact]
    public async Task RemoveAsync_one_of_many_leaves_others()
    {
        var (index, _) = CreateIndex();
        await index.AddAsync("a");
        await index.AddAsync("b");
        await index.RemoveAsync("a");
        var ids = await index.GetAllIdsAsync();
        Assert.Single(ids);
        Assert.Equal("b", ids[0]);
    }
}
