using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Core.Extensions;
using OpenClaw.Core.SignalR;

namespace OpenClaw.Tests.SignalR;

/// <summary>
/// 在随机本地端口启动 Kestrel，注册 OpenClaw + SignalR + 桥接，用于端到端 Hub 集成测试。
/// </summary>
/// <remarks>
/// <para>
/// <paramref name="registerFakeRpc"/> 为 <c>true</c>（默认）时，在 <c>AddOpenClawSignalRGateway</c> 之后注册
/// <see cref="FakeOpenClawGatewayRpc"/>，覆盖 <see cref="OpenClawGatewayRpc"/>，使 Hub 的
/// <c>invokeRpcAsync</c> / <c>chatAsync</c> 不依赖真实 WebSocket；调试集成测试时请保持默认，否则会看到
/// <c>Gateway is not connected</c>（真实 <see cref="GatewayClient"/> 未建连时 <see cref="IOpenClawGatewayRpc.IsConnected"/> 为 false）。
/// </para>
/// <para>
/// 网关事件广播测试依赖 <see cref="GatewayClient.Events"/> 与 Hub 桥接，与是否注册 Fake RPC 无关；广播类用例也应使用
/// <paramref name="registerFakeRpc"/>=<c>true</c>，便于在调试时随意调用 Hub RPC。
/// </para>
/// </remarks>
internal sealed class SignalRTestHost : IAsyncDisposable
{
    private WebApplication? _app;

    public Uri BaseUri { get; private init; } = null!;

    public WebApplication App => _app ?? throw new InvalidOperationException("Host not started.");

    /// <param name="registerFakeRpc">
    /// 为 <c>true</c> 时注册 <see cref="FakeOpenClawGatewayRpc"/>，Hub RPC 不依赖网关 WebSocket；为 <c>false</c> 时使用默认
    /// <see cref="OpenClawGatewayRpc"/>，未对 <see cref="GatewayClient"/> 建连时 <c>invokeRpcAsync</c> 会报 Gateway is not connected。
    /// </param>
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

        builder.Services.AddSingleton<IGatewayEventAudienceResolver, AllClientsGatewayEventAudienceResolver>();

        builder.Services.AddOpenClawSignalRGateway<OpenClawGatewayHubAllowAnonymous>(configure: o =>
            {
                o.EnableBackgroundConnect = false;
                configureSignalR?.Invoke(o);
            })
            .UseMemoryStore();

        // 须在 AddOpenClawSignalRGateway 之后注册，以覆盖 TryAdd 注入的 OpenClawGatewayRpc（否则 Hub RPC 走真实 GatewayClient，未 Connect 则 IsConnected=false）。
        if (registerFakeRpc)
            builder.Services.AddSingleton<IOpenClawGatewayRpc>(_ => new FakeOpenClawGatewayRpc());

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
