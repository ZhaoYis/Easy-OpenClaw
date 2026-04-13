using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace OpenClaw.Core.SignalR;

internal static class OpenClawSignalRJwtConfiguration
{
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

            var accessToken = context.Request.Query["access_token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(accessToken))
                context.Token = accessToken;
        };
    }
}
