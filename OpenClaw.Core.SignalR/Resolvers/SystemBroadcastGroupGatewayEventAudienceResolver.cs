using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 将网关事件推送到 <see cref="OpenClawSignalROptions.SystemBroadcastGroupName"/> 组；与
/// <see cref="OpenClawGatewayHubBase"/> 建连时加入的组一致，受众由连接时的授权与分组决定，不依赖
/// <see cref="GatewayEventAudienceResolveContext.Event"/> 中的用户字段。
/// </summary>
public sealed class SystemBroadcastGroupGatewayEventAudienceResolver : IGatewayEventAudienceResolver
{
    /// <summary>
    /// 根据上下文决定推送到哪些连接；本实现推送到 <see cref="OpenClawSignalROptions.SystemBroadcastGroupName"/> 组。
    /// </summary>
    /// <param name="context">含 <see cref="GatewayEventAudienceResolveContext.Options"/> 与 <see cref="GatewayEventAudienceResolveContext.Clients"/></param>
    /// <param name="target">组代理；组名为空时为 null</param>
    /// <returns>组名非空白时为 true</returns>
    public bool TryResolveClients(GatewayEventAudienceResolveContext context, [NotNullWhen(true)] out IClientProxy? target)
    {
        var name = context.Options.SystemBroadcastGroupName;
        if (string.IsNullOrWhiteSpace(name))
        {
            target = null;
            return false;
        }

        target = context.Clients.Group(name);
        return true;
    }
}
