using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Core.Extensions;
using OpenClaw.Core.SignalR;

namespace OpenClaw.Tests.SignalR;

/// <summary>带 JWT + <see cref="OpenClawGatewayHub"/> 的集成测试宿主。</summary>
internal sealed class JwtSignalRTestHost : IAsyncDisposable
{
    private WebApplication? _app;

    public Uri BaseUri { get; private init; } = null!;

    public WebApplication App => _app ?? throw new InvalidOperationException("Host not started.");

    public static async Task<JwtSignalRTestHost> StartAsync(
        Action<OpenClawSignalROptions>? configureSignalR = null,
        CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(static k => k.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();

        builder.Services.AddOpenClaw(o =>
        {
            o.Url = "ws://127.0.0.1:1";
            o.Token = "jwt-integration-test";
            o.KeyFilePath = null;
        });

        builder.Services.AddAuthorization();
        builder.Services.AddOpenClawSignalRAuthentication();
        builder.Services.AddSignalR(o => o.EnableDetailedErrors = true);
        builder.Services.AddSingleton<IOpenClawGatewayRpc>(_ => new FakeOpenClawGatewayRpc());
        builder.Services.AddSingleton<IGatewayEventAudienceResolver, PayloadTargetUserAudienceResolver>();

        builder.Services.AddOpenClawSignalRGateway<OpenClawGatewayHub>(configure: o =>
            {
                o.EnableBackgroundConnect = false;
                o.Jwt.SigningKeyBase64 = TestJwtTokens.SymmetricKeyBase64;
                o.Jwt.Issuer = TestJwtTokens.Issuer;
                o.Jwt.Audience = TestJwtTokens.Audience;
                o.TierClaimType = "tier";
                configureSignalR?.Invoke(o);
            })
            .UseMemoryStore();

        builder.Services.AddControllers().AddApplicationPart(typeof(TestOpenClawOpsController).Assembly);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        // MapControllers 须在 MapHub 之前：否则 Hub 断开时 OnDisconnected 可能无法正确执行，运营快照残留。
        app.MapControllers();
        app.MapHub<OpenClawGatewayHub>("/hubs/openclaw").RequireAuthorization();
        await app.StartAsync(cancellationToken).ConfigureAwait(false);

        var baseUri = SignalRIntegrationTestHostUtilities.ResolveHttpBaseUri(app);
        return new JwtSignalRTestHost { _app = app, BaseUri = baseUri };
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