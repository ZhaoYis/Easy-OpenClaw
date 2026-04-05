using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenClaw.AutoApprove.Core.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 AutoApprove 核心服务：配置选项 + 后台轮询服务。
    /// </summary>
    public static IServiceCollection AddAutoApprove(this IServiceCollection services, IConfigurationSection section)
    {
        services.AddOptions<AutoApproveOptions>()
            .Bind(section);

        services.AddHostedService<AutoApproveService>();

        return services;
    }
}
