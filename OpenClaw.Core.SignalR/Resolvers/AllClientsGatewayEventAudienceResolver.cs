using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 将网关事件推送给全部 SignalR 连接（<see cref="IHubClients.All"/>）。
/// </summary>
/// <remarks>
/// 存在严重串消息与泄露风险，仅用于开发或明确全员旁路的场景；生产环境应使用自定义 <see cref="IGatewayEventAudienceResolver"/> 或基于建连分组的解析器。
/// </remarks>
public sealed class AllClientsGatewayEventAudienceResolver : IGatewayEventAudienceResolver
{
    /// <summary>
    /// 根据上下文决定推送到哪些连接；本实现推送到 <see cref="IHubClients.All"/>。
    /// </summary>
    /// <param name="context">须含有效的 <see cref="GatewayEventAudienceResolveContext.Clients"/></param>
    /// <param name="target"><see cref="IHubClients.All"/></param>
    /// <returns>始终为 true（存在泄露与串消息风险，仅开发/旁路使用）</returns>
    public bool TryResolveClients(GatewayEventAudienceResolveContext context, [NotNullWhen(true)] out IClientProxy? target)
    {
        target = context.Clients.All;
        return true;
    }
}
