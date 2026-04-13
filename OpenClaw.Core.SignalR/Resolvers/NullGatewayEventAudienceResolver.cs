using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 默认受众解析器：不解析任何目标（零广播，防误发）。
/// </summary>
public sealed class NullGatewayEventAudienceResolver : IGatewayEventAudienceResolver
{
    public bool TryResolveClients(GatewayEventAudienceResolveContext context, [NotNullWhen(true)] out IClientProxy? target)
    {
        target = null;
        return false;
    }
}
