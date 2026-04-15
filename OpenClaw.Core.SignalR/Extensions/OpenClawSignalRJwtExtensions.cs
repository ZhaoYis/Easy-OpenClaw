using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 为 SignalR（WebSocket）注册 JWT Bearer：支持 <c>Authorization: Bearer</c>、Hub 路径下 query <c>access_token</c> 与请求头 <c>access_token</c>。
/// </summary>
public static class OpenClawSignalRJwtExtensions
{
    /// <summary>
    /// 注册 <c>Authentication</c> 默认方案为 Bearer，并绑定 <see cref="OpenClawSignalROptions.Jwt"/> 与 Hub 路径下 query/头 <c>access_token</c> 补全逻辑。
    /// 宿主仍需 <c>services.AddAuthorization()</c>、<c>app.UseAuthentication()</c>、<c>app.UseAuthorization()</c>，
    /// 以及对 Hub 使用 <c>RequireAuthorization()</c>（或使用带 <c>[Authorize]</c> 的 <see cref="OpenClawGatewayHub"/>）。
    /// </summary>
    public static IServiceCollection AddOpenClawSignalRAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<OpenClawSignalROptions>>((jwtOptions, sr) =>
            {
                OpenClawSignalRJwtConfiguration.Apply(jwtOptions, sr.Value.Jwt);
                OpenClawSignalRJwtConfiguration.AttachAccessTokenFromQuery(jwtOptions,
                    string.IsNullOrWhiteSpace(sr.Value.SignalRHubPathPrefix)
                        ? new PathString("/hubs")
                        : new PathString(sr.Value.SignalRHubPathPrefix));
            });

        return services;
    }
}
