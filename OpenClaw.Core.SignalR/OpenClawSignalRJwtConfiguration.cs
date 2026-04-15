using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 将 <see cref="OpenClawSignalRJwtOptions"/> 应用到 <see cref="JwtBearerOptions"/>，并挂载 SignalR Hub 路径下从 query / 自定义头补充 token。
/// </summary>
internal static class OpenClawSignalRJwtConfiguration
{
    /// <summary>
    /// 配置 Authority/Audience；若配置了 <see cref="OpenClawSignalRJwtOptions.SigningKeyBase64"/> 则使用对称密钥校验。
    /// </summary>
    public static void Apply(JwtBearerOptions options, OpenClawSignalRJwtOptions jwt)
    {
        options.RequireHttpsMetadata = jwt.RequireHttpsMetadata;

        if (!string.IsNullOrWhiteSpace(jwt.Authority))
            options.Authority = jwt.Authority.Trim();

        if (!string.IsNullOrWhiteSpace(jwt.Audience))
            options.Audience = jwt.Audience.Trim();

        if (!string.IsNullOrWhiteSpace(jwt.SigningKeyBase64))
        {
            var keyBytes = Convert.FromBase64String(jwt.SigningKeyBase64.Trim());
            var key = new SymmetricSecurityKey(keyBytes);
            options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = key,
                ValidIssuer = string.IsNullOrWhiteSpace(jwt.Issuer) ? null : jwt.Issuer.Trim(),
                ValidAudience = string.IsNullOrWhiteSpace(jwt.Audience) ? null : jwt.Audience.Trim(),
                ValidateIssuer = !string.IsNullOrWhiteSpace(jwt.Issuer),
                ValidateAudience = !string.IsNullOrWhiteSpace(jwt.Audience),
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            };
        }
    }

    /// <summary>
    /// 在 Hub 路径下，若 <c>context.Token</c> 仍为空，则依次尝试：
    /// query <c>access_token</c>、请求头 <c>access_token</c>（可选带 <c>Bearer </c> 前缀）。
    /// 用于 WebSocket negotiate/连接阶段无法使用标准 <c>Authorization: Bearer</c> 或需与 query 并存时的补全。
    /// </summary>
    /// <param name="options">Bearer 选项（会串联已有 <c>OnMessageReceived</c>）</param>
    /// <param name="hubPathPrefix">与 <see cref="OpenClawSignalROptions.SignalRHubPathPrefix"/> 一致的路径前缀</param>
    public static void AttachAccessTokenFromQuery(
        JwtBearerOptions options,
        PathString hubPathPrefix)
    {
        var previous = options.Events.OnMessageReceived;
        options.Events.OnMessageReceived = async context =>
        {
            if (previous is not null)
                await previous(context).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(context.Token))
                return;

            var path = context.HttpContext.Request.Path;
            if (!path.StartsWithSegments(hubPathPrefix))
                return;

            var fromQuery = context.Request.Query["access_token"].FirstOrDefault();
            var token = NormalizeAccessToken(fromQuery);
            if (token is null)
            {
                var fromHeader = context.Request.Headers["access_token"].FirstOrDefault();
                token = NormalizeAccessToken(fromHeader);
            }

            if (!string.IsNullOrEmpty(token))
                context.Token = token;
        };
    }

    /// <summary>去掉首尾空白；若以 <c>Bearer </c> 开头（不区分大小写）则剥掉前缀。</summary>
    private static string? NormalizeAccessToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        const string prefix = $"{JwtBearerDefaults.AuthenticationScheme} ";
        if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            t = t[prefix.Length..].Trim();

        return string.IsNullOrEmpty(t) ? null : t;
    }
}