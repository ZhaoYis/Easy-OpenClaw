using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// <see cref="IOpenClawSystemBroadcastSender{THub}"/>、<see cref="OpenClawGatewayEventBroadcaster{THub}"/>、
    /// <see cref="OpenClawGatewayConnectHostedService"/>。
    /// </summary>
    /// <returns>
    /// 构建器；须继续调用 <see cref="OpenClawSignalRGatewayBuilder.UseMemoryConnectionPresence"/>、
    /// <see cref="OpenClawSignalRGatewayBuilder.UseHybridConnectionPresence"/> 或
    /// <see cref="OpenClawSignalRGatewayBuilder.UseCustomConnectionPresence"/> /
    /// <see cref="OpenClawSignalRGatewayBuilder.UseConnectionPresenceStore{TStore}"/> 以注册
    /// <see cref="IOpenClawSignalRConnectionPresenceStore"/>。
    /// </returns>
    /// <remarks>
    /// 在调用本方法<strong>之前</strong>注册自定义 <see cref="IGatewayEventAudienceResolver"/> 可替换默认的
    /// <see cref="NullGatewayEventAudienceResolver"/>（零广播）。常见选项：<see cref="SystemBroadcastGroupGatewayEventAudienceResolver"/>
    /// （与 Hub 建连加入的 <see cref="OpenClawSignalROptions.SystemBroadcastGroupName"/> 一致）、
    /// <see cref="AllPresenceConnectionsGatewayEventAudienceResolver"/>（按运营快照扇出全部连接，成本较高）、
    /// 集成测试或开发可注册 <see cref="AllClientsGatewayEventAudienceResolver"/>（全员推送，有风险）。
    /// </remarks>
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
        services.AddHostedService<OpenClawGatewayConnectHostedService>();
        services.AddHostedService<OpenClawGatewayEventBroadcaster<THub>>();
        return new OpenClawSignalRGatewayBuilder(services);
    }
}
