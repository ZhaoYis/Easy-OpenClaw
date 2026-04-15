using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 将网关事件推送到当前 <see cref="IOpenClawSignalRConnectionPresenceStore"/> 中的全部连接 id（
/// <see cref="IHubClients"/> 的多连接重载）。快照在用户建连时由 Hub 写入，
/// <see cref="OpenClawSignalRConnectionSnapshot.UserId"/> 等来自当时 Claims，而非 <c>GatewayEvent</c>。
/// </summary>
/// <remarks>
/// 每条事件会触发广播器拉取全量快照（<see cref="IGatewayEventAudienceResolver.RequiresConnectionSnapshotEnumeration"/>），
/// 连接数大或 Hybrid 存储时成本较高；多数场景优先使用 <see cref="SystemBroadcastGroupGatewayEventAudienceResolver"/>。
/// </remarks>
public sealed class AllPresenceConnectionsGatewayEventAudienceResolver : IGatewayEventAudienceResolver
{
    public bool RequiresConnectionSnapshotEnumeration => true;

    /// <summary>
    /// 根据上下文决定推送到哪些连接；本实现使用 <see cref="GatewayEventAudienceResolveContext.ConnectionSnapshots"/> 中的全部连接 id。
    /// </summary>
    /// <param name="context">须已由协调器填入非空 <see cref="GatewayEventAudienceResolveContext.ConnectionSnapshots"/></param>
    /// <param name="target"><see cref="IHubClients.Clients(string[])"/> 多连接代理；快照为空时为 null</param>
    /// <returns>快照非空时为 true</returns>
    public bool TryResolveClients(GatewayEventAudienceResolveContext context, [NotNullWhen(true)] out IClientProxy? target)
    {
        target = null;
        if (context.ConnectionSnapshots is not { Count: > 0 })
            return false;

        var ids = new string[context.ConnectionSnapshots.Count];
        for (var i = 0; i < context.ConnectionSnapshots.Count; i++)
            ids[i] = context.ConnectionSnapshots[i].ConnectionId;

        target = context.Clients.Clients(ids);
        return true;
    }
}
