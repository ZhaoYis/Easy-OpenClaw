using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenClaw.AutoApprove.Core.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 AutoApprove 核心服务：绑定 <see cref="AutoApproveOptions"/> 并注册 <see cref="AutoApproveService"/> 为 <see cref="Microsoft.Extensions.Hosting.IHostedService"/>。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="section">通常为 <c>configuration.GetSection(AutoApproveOptions.SectionName)</c></param>
    /// <returns>链式 <paramref name="services"/></returns>
    public static IServiceCollection AddAutoApprove(this IServiceCollection services, IConfigurationSection section)
    {
        services.AddOptions<AutoApproveOptions>()
            .Bind(section);

        services.AddHostedService<AutoApproveService>();

        return services;
    }
}
