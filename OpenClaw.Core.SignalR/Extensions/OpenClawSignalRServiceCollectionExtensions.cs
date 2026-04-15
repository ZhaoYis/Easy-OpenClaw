using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenClaw.Core.Client;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 注册 OpenClaw SignalR 桥接：RPC 抽象、受众解析、网关事件广播、系统广播发送器、可选的后台连接。
/// 宿主仍需自行调用 <c>services.AddSignalR()</c> 与 <c>app.MapHub&lt;THub&gt;(...)</c>；
/// 且须在 <c>Build</c> 之前调用 <c>AddSignalR</c>，以便解析 <c>IHubContext&lt;THub&gt;</c>。
/// </summary>
public static class OpenClawSignalRServiceCollectionExtensions
{
    /// <summary>
    /// 注册 <see cref="IOpenClawGatewayRpc"/>、<see cref="IGatewayEventAudienceResolver"/>（若尚未注册）、
    /// <see cref="IUserIdProvider"/>、<see cref="IOpenClawSignalROperationService{THub}"/>、
    /// <see cref="IOpenClawSystemBroadcastSender{THub}"/>、<see cref="IOpenClawSignalRGatewayHubBridge"/>（Hub 建连后挂接网关与事件推送）。
    /// </summary>
    /// <returns>
    /// 构建器；须继续调用 <see cref="OpenClawSignalRGatewayBuilder.UseMemoryStore"/>、
    /// <see cref="OpenClawSignalRGatewayBuilder.UseHybridStore"/> 或
    /// <see cref="OpenClawSignalRGatewayBuilder.UseCustomStore{TStore}"/> 以注册
    /// <see cref="IOpenClawSignalRConnectionPresenceStore"/>。
    /// </returns>
    /// <remarks>
    /// 在调用本方法<strong>之前</strong>注册自定义 <see cref="IGatewayEventAudienceResolver"/> 可替换默认的
    /// <see cref="NullGatewayEventAudienceResolver"/>（零广播）。常见选项：<see cref="SystemBroadcastGroupGatewayEventAudienceResolver"/>
    /// （与 Hub 建连加入的 <see cref="OpenClawSignalROptions.SystemBroadcastGroupName"/> 一致）、
    /// <see cref="AllPresenceConnectionsGatewayEventAudienceResolver"/>（按运营快照扇出全部连接，成本较高）、
    /// 集成测试或开发可注册 <see cref="AllClientsGatewayEventAudienceResolver"/>（全员推送，有风险）。
    /// </remarks>
    /// <param name="services">宿主服务集合</param>
    /// <param name="configurationSection">可选；绑定 <see cref="OpenClawSignalROptions.SectionName"/> 等配置节</param>
    /// <param name="configure">可选；代码方式覆盖选项</param>
    /// <typeparam name="THub">与后续 <c>MapHub&lt;THub&gt;</c> 一致的 Hub 类型</typeparam>
    public static OpenClawSignalRGatewayBuilder AddOpenClawSignalRGateway<THub>(
        this IServiceCollection services,
        IConfigurationSection? configurationSection = null,
        Action<OpenClawSignalROptions>? configure = null)
        where THub : Hub
    {
        var optionsBuilder = services.AddOptions<OpenClawSignalROptions>();
        if (configurationSection is not null)
            optionsBuilder.Bind(configurationSection);

        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.TryAddSingleton<IOpenClawGatewayRpc, OpenClawGatewayRpc>();
        services.TryAddSingleton<IGatewayEventAudienceResolver, NullGatewayEventAudienceResolver>();
        services.TryAddSingleton<IUserIdProvider, OpenClawSignalRUserIdProvider>();
        services.AddSingleton<IOpenClawSignalROperationService<THub>, OpenClawSignalROperationService<THub>>();
        services.AddSingleton<IOpenClawSystemBroadcastSender<THub>, OpenClawSystemBroadcastSender<THub>>();
        services.AddSingleton<OpenClawSignalRGatewayHubBridgeCoordinator<THub>>();
        services.AddSingleton<IOpenClawSignalRGatewayHubBridge>(static sp =>
            sp.GetRequiredService<OpenClawSignalRGatewayHubBridgeCoordinator<THub>>());
        services.AddSingleton<IGatewayClientConnectionResolver, OpenClawSignalRGatewayClientConnectionResolver>();
        services.AddSingleton<IGatewayClientStateStore, OpenClawSignalRGatewayClientStateStore>();
        return new OpenClawSignalRGatewayBuilder(services);
    }
}