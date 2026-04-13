using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace OpenClaw.Tests.SignalR;

internal static class SignalRIntegrationTestHostUtilities
{
    public static Uri ResolveHttpBaseUri(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var feat = server.Features.Get<IServerAddressesFeature>()
                   ?? throw new InvalidOperationException("IServerAddressesFeature is not available.");
        var addr = feat.Addresses.FirstOrDefault(static a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException("No http:// listen address.");
        return new Uri(addr);
    }
}
