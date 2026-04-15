using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Client;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Extensions;

/// <summary>
/// OpenClaw 服务注册扩展方法。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 OpenClaw Gateway 核心服务。
    /// <para>生命周期说明：</para>
    /// <list type="bullet">
    ///   <item><see cref="DeviceIdentity"/> — Singleton：设备密钥对在整个应用生命周期中唯一</item>
    ///   <item><see cref="EventRouter"/> — Singleton：事件处理器注册表跨重连保持</item>
    ///   <item><see cref="GatewayRequestManager"/> — Singleton：追踪所有进行中的请求</item>
    ///   <item><see cref="IGatewayClientStateStore"/> / <see cref="IGatewayClientConnectionResolver"/> — Singleton：默认可改为按用户注册工厂</item>
    ///   <item><see cref="GatewayClient"/> — Singleton：维护 WebSocket 连接状态（亦可手动 new 多实例）</item>
    ///   <item><see cref="GatewayEventSubscriber"/> — Singleton：事件订阅管理器与 Client 生命周期一致</item>
    /// </list>
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddOpenClaw(this IServiceCollection services, Action<GatewayOptions> configure)
    {
        services.AddOptions<GatewayOptions>()
            .Configure(configure)
            .Validate(o => !string.IsNullOrWhiteSpace(o.Url), "OpenClaw Gateway URL 不能为空")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Token) || !string.IsNullOrWhiteSpace(o.Password),
                "OpenClaw Gateway Token 或 Password 至少需要配置一项")
            .ValidateOnStart();

        RegisterCoreServices(services);
        return services;
    }

    /// <summary>
    /// 注册 OpenClaw Gateway 核心服务，从 <see cref="GatewayOptions.SectionName"/> 配置节绑定。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configurationSection">配置节（通常为 configuration.GetSection("OpenClaw")）</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddOpenClaw(this IServiceCollection services, IConfigurationSection configurationSection)
    {
        services.AddOptions<GatewayOptions>()
            .Bind(configurationSection)
            .Validate(o => !string.IsNullOrWhiteSpace(o.Url), "OpenClaw Gateway URL 不能为空")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Token) || !string.IsNullOrWhiteSpace(o.Password),
                "OpenClaw Gateway Token 或 Password 至少需要配置一项")
            .ValidateOnStart();

        RegisterCoreServices(services);
        return services;
    }

    /// <summary>
    /// 将 <see cref="GatewayEventSubscriber"/> 注册为单例；不会自动调用 <see cref="GatewayEventSubscriber.RegisterAll"/>，
    /// 宿主须在启动流程中自行调用以挂载网关事件处理器。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>链式 <paramref name="services"/></returns>
    public static IServiceCollection UseOpenClawEventSubscriber(this IServiceCollection services)
    {
        services.AddSingleton<GatewayEventSubscriber>();
        return services;
    }

    /// <summary>
    /// 注册 <see cref="HealthMonitorService"/> 后台健康监控服务。
    /// 前置依赖 <see cref="UseOpenClawEventSubscriber"/>（被动事件监听需要 <see cref="GatewayEventSubscriber"/>）。
    /// 通过 <see cref="GatewayOptions.EnableHealthMonitor"/> 控制是否真正启动轮询，
    /// 但注册本身始终完成，以便应用层可通过 DI 获取 <see cref="HealthMonitorService"/> 实例。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>链式 <paramref name="services"/></returns>
    public static IServiceCollection UseOpenClawHealthMonitor(this IServiceCollection services)
    {
        services.AddSingleton<HealthMonitorService>();
        services.AddHostedService(sp => sp.GetRequiredService<HealthMonitorService>());
        return services;
    }

    /// <summary>
    /// 注册核心单例：<see cref="DeviceIdentity"/>、<see cref="EventRouter"/>、<see cref="GatewayRequestManager"/>、
    /// 默认状态存储与连接解析器、<see cref="GatewayClient"/>。
    /// </summary>
    /// <param name="services">服务集合</param>
    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.AddSingleton<DeviceIdentity>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<GatewayOptions>>().Value;
            return DeviceIdentity.LoadOrCreate(opts.KeyFilePath);
        });

        services.AddSingleton<EventRouter>();
        services.AddSingleton<GatewayRequestManager>();
        services.AddSingleton<IGatewayClientStateStore, FileGatewayClientStateStore>();
        services.AddSingleton<IGatewayClientConnectionResolver, DefaultGatewayClientConnectionResolver>();
        services.AddSingleton<GatewayClient>();
    }
}