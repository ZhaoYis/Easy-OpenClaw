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
    ///   <item><see cref="GatewayClient"/> — Singleton：维护单一 WebSocket 连接状态</item>
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
            .Validate(o => !string.IsNullOrWhiteSpace(o.Token), "OpenClaw Gateway Token 不能为空")
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
            .Validate(o => !string.IsNullOrWhiteSpace(o.Token), "OpenClaw Gateway Token 不能为空")
            .ValidateOnStart();

        RegisterCoreServices(services);
        return services;
    }

    /// <summary>
    /// 注册 <see cref="GatewayEventSubscriber"/> 并自动调用 <see cref="GatewayEventSubscriber.RegisterAll"/>。
    /// 在需要完整事件日志输出的场景下使用。
    /// </summary>
    public static IServiceCollection UseOpenClawEventSubscriber(this IServiceCollection services)
    {
        services.AddSingleton<GatewayEventSubscriber>();
        return services;
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.AddSingleton<DeviceIdentity>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<GatewayOptions>>().Value;
            return DeviceIdentity.LoadOrCreate(opts.KeyFilePath);
        });

        services.AddSingleton<EventRouter>();
        services.AddSingleton<GatewayRequestManager>();
        services.AddSingleton<GatewayClient>();
    }
}