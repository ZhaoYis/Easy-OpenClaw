using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 默认受众解析器：不解析任何目标（零广播，防误发）。
/// </summary>
public sealed class NullGatewayEventAudienceResolver : IGatewayEventAudienceResolver
{
    /// <summary>
    /// 根据上下文决定推送到哪些连接；本实现不解析任何目标。
    /// </summary>
    /// <param name="context">当前网关事件与客户端集合（未使用）</param>
    /// <param name="target">始终为 null</param>
    /// <returns>始终为 false，广播器不调用 <see cref="IClientProxy.SendAsync"/>。</returns>
    public bool TryResolveClients(GatewayEventAudienceResolveContext context, [NotNullWhen(true)] out IClientProxy? target)
    {
        target = null;
        return false;
    }
}
