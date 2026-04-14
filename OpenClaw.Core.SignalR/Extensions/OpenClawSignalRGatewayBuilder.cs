using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// <see cref="OpenClawSignalRServiceCollectionExtensions.AddOpenClawSignalRGateway{THub}"/> 的链式构建器：
/// 须任选其一注册 <see cref="IOpenClawSignalRConnectionPresenceStore"/>（内存、Hybrid 或自定义）。
/// </summary>
public sealed class OpenClawSignalRGatewayBuilder
{
    internal OpenClawSignalRGatewayBuilder(IServiceCollection services) => Services = services;

    /// <summary>当前服务集合（与宿主共用同一实例）。</summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// 使用进程内 <see cref="IMemoryCache"/> 与锁保护的连接 id 集合（非分布式）。
    /// </summary>
    public IServiceCollection UseMemoryStore()
    {
        ClearPresenceRegistrations();
        Services.AddMemoryCache();
        Services.TryAddSingleton<OpenClawSignalRMemoryConnectionPresenceStore>();
        Services.AddSingleton<IOpenClawSignalRConnectionPresenceStore>(static sp =>
            sp.GetRequiredService<OpenClawSignalRMemoryConnectionPresenceStore>());
        return Services;
    }

    /// <summary>
    /// 使用 <see cref="HybridCache"/> 存载荷、<see cref="IDistributedCache"/> 存连接 id 索引（多实例须共享同一分布式后端）。
    /// </summary>
    /// <param name="configureCachingInfrastructure">
    /// 可选；用于注册共享 <see cref="IDistributedCache"/>（例如 <c>AddStackExchangeRedisCache</c>）。
    /// 请勿在此调用 <c>AddHybridCache</c>，由本方法在回调之后统一注册。
    /// </param>
    /// <param name="configureHybridCache">可选；传给 <c>AddHybridCache</c> 的选项配置。</param>
    public IServiceCollection UseHybridStore(
        Action<IServiceCollection>? configureCachingInfrastructure = null,
        Action<HybridCacheOptions>? configureHybridCache = null)
    {
        ClearPresenceRegistrations();
        configureCachingInfrastructure?.Invoke(Services);
        Services.AddDistributedMemoryCache();
        if (configureHybridCache is not null)
            Services.AddHybridCache(configureHybridCache);
        else
            Services.AddHybridCache();

        Services.AddSingleton<IOpenClawSignalRConnectionPresenceStore>(static sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpenClawSignalROptions>>();
            var hybrid = sp.GetRequiredService<HybridCache>();
            var index = new OpenClawSignalRDistributedConnectionIdIndex(
                sp.GetRequiredService<IDistributedCache>(),
                options);
            return new OpenClawSignalRHybridConnectionPresenceStore(hybrid, index, options);
        });
        return Services;
    }

    /// <summary>使用自定义 <typeparamref name="TStore"/> 作为 <see cref="IOpenClawSignalRConnectionPresenceStore"/> 实现。</summary>
    public IServiceCollection UseCustomStore<TStore>()
        where TStore : class, IOpenClawSignalRConnectionPresenceStore
    {
        ClearPresenceRegistrations();
        Services.AddSingleton<IOpenClawSignalRConnectionPresenceStore, TStore>();
        return Services;
    }

    private void ClearPresenceRegistrations()
    {
        Services.RemoveAll<IOpenClawSignalRConnectionPresenceStore>();
        Services.RemoveAll<OpenClawSignalRMemoryConnectionPresenceStore>();
    }
}