using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Core.Extensions;
using OpenClaw.Core.SignalR;

namespace OpenClaw.Tests.SignalR;

/// <summary>
/// 在随机本地端口启动 Kestrel，注册 OpenClaw + SignalR + 桥接，用于端到端 Hub 集成测试。
/// </summary>
internal sealed class SignalRTestHost : IAsyncDisposable
{
    private WebApplication? _app;

    public Uri BaseUri { get; private init; } = null!;

    public WebApplication App => _app ?? throw new InvalidOperationException("Host not started.");

    public static async Task<SignalRTestHost> StartAsync(
        bool registerFakeRpc = true,
        Action<OpenClawSignalROptions>? configureSignalR = null,
        CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(static k => k.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();

        builder.Services.AddOpenClaw(o =>
        {
            o.Url = "ws://127.0.0.1:1";
            o.Token = "integration-test";
            o.KeyFilePath = null;
        });

        builder.Services.AddSignalR(o => o.EnableDetailedErrors = true);

        if (registerFakeRpc)
            builder.Services.AddSingleton<IOpenClawGatewayRpc>(_ => new FakeOpenClawGatewayRpc());

        builder.Services.AddSingleton<IGatewayEventAudienceResolver, AllClientsGatewayEventAudienceResolver>();

        builder.Services.AddOpenClawSignalRGateway<OpenClawGatewayHubAllowAnonymous>(configure: o =>
            {
                o.EnableBackgroundConnect = false;
                configureSignalR?.Invoke(o);
            })
            .UseMemoryConnectionPresence();

        var app = builder.Build();
        app.MapHub<OpenClawGatewayHubAllowAnonymous>("/hubs/openclaw");
        await app.StartAsync(cancellationToken).ConfigureAwait(false);

        var baseUri = SignalRIntegrationTestHostUtilities.ResolveHttpBaseUri(app);
        return new SignalRTestHost { _app = app, BaseUri = baseUri };
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
