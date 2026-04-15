using OpenClaw.Core.Models;
using OpenClaw.Core.SignalR;
using Xunit;

namespace OpenClaw.Tests.SignalR;

/// <summary>
/// <see cref="OpenClawSignalRGatewayClientConnectionResolver"/> 应对任何 state 回退到配置中的默认 URL/指纹
///（返回空白/ null，由 <see cref="OpenClaw.Core.Client.GatewayClient"/> 合并）。
/// </summary>
public sealed class OpenClawSignalRGatewayClientConnectionResolverTests
{
    [Fact]
    public async Task Returns_blank_url_and_null_tls_so_gateway_client_falls_back_to_options()
    {
        var resolver = new OpenClawSignalRGatewayClientConnectionResolver();
        var options = new GatewayOptions
        {
            Url = "wss://gw.prod/ws",
            TlsFingerprint = "aa:bb",
            Token = "t",
        };
        var ctx = new OpenClawSignalRGatewayHubBridgeContext("cid-1", "user-a");

        var url = await resolver.ResolveWebSocketUrlAsync(ctx, options, CancellationToken.None);
        var tls = await resolver.ResolveTlsFingerprintAsync(ctx, options, CancellationToken.None);

        Assert.Equal(string.Empty, url);
        Assert.Null(tls);
    }
}
