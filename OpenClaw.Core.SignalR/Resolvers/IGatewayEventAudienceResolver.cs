using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 将单条网关事件解析为 <see cref="IClientProxy"/>；未解析成功时不应推送，避免消息串发。
/// </summary>
public interface IGatewayEventAudienceResolver
{
    /// <summary>
    /// 为 true 时 <see cref="OpenClawSignalRGatewayHubBridgeCoordinator{THub}"/> 在调用前拉取
    /// <see cref="IOpenClawSignalRConnectionPresenceStore"/> 快照并填入
    /// <see cref="GatewayEventAudienceResolveContext.ConnectionSnapshots"/>（有 O(n) 成本，Hybrid 下可能触达分布式索引）。
    /// </summary>
    bool RequiresConnectionSnapshotEnumeration => false;

    /// <summary>
    /// 根据上下文决定推送到哪些连接（如 <c>context.Clients.Group(...)</c>、<c>context.Clients.User(id)</c> 或
    /// 使用 <see cref="GatewayEventAudienceResolveContext.ConnectionSnapshots"/> 构造 <c>context.Clients.Clients(...)</c>）。
    /// </summary>
    /// <param name="context">当前网关事件、<see cref="IHubClients"/>、选项与可选连接快照</param>
    /// <param name="target">解析成功时的发送目标</param>
    /// <returns>若返回 false，广播器不调用 <see cref="IClientProxy.SendAsync"/>。</returns>
    bool TryResolveClients(GatewayEventAudienceResolveContext context, [NotNullWhen(true)] out IClientProxy? target);
}
