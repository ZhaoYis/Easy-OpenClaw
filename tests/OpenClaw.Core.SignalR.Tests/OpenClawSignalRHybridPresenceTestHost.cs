using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Core.Extensions;
using OpenClaw.Core.SignalR;

namespace OpenClaw.Tests.SignalR;

/// <summary>JWT + <see cref="OpenClawGatewayHub"/> + Hybrid 连接运营存储。</summary>
internal sealed class OpenClawSignalRHybridPresenceTestHost : IAsyncDisposable
{
    private WebApplication? _app;

    public Uri BaseUri { get; private init; } = null!;

    public WebApplication App => _app ?? throw new InvalidOperationException("Host not started.");

    public static async Task<OpenClawSignalRHybridPresenceTestHost> StartAsync(
        CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(static k => k.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();

        builder.Services.AddOpenClaw(o =>
        {
            o.Url = "ws://127.0.0.1:1";
            o.Token = "hybrid-presence-test";
            o.KeyFilePath = null;
        });

        builder.Services.AddAuthorization();
        builder.Services.AddOpenClawSignalRAuthentication();
        builder.Services.AddSignalR(o => o.EnableDetailedErrors = true);
        builder.Services.AddSingleton<IGatewayEventAudienceResolver, PayloadTargetUserAudienceResolver>();

        builder.Services.AddOpenClawSignalRGateway<OpenClawGatewayHub>(configure: o =>
            {
                o.EnableBackgroundConnect = false;
                o.Jwt.SigningKeyBase64 = TestJwtTokens.SymmetricKeyBase64;
                o.Jwt.Issuer = TestJwtTokens.Issuer;
                o.Jwt.Audience = TestJwtTokens.Audience;
                o.TierClaimType = "tier";
            })
            .UseHybridStore();

        builder.Services.AddSingleton<IOpenClawGatewayRpc>(_ => new FakeOpenClawGatewayRpc());

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHub<OpenClawGatewayHub>("/hubs/openclaw").RequireAuthorization();
        await app.StartAsync(cancellationToken).ConfigureAwait(false);

        var baseUri = SignalRIntegrationTestHostUtilities.ResolveHttpBaseUri(app);
        return new OpenClawSignalRHybridPresenceTestHost { _app = app, BaseUri = baseUri };
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is null)
            return;
        await _app.StopAsync().ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
        _app = null;
    }
}
