using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 将网关事件推送到建立桥接订阅时的「当前用户」：使用
/// <see cref="GatewayEventAudienceResolveContext.State"/> 中的 <see cref="OpenClawSignalRGatewayHubBridgeContext"/>（与
/// <see cref="OpenClawSignalRGatewayHubBridgeCoordinator{THub}"/> 传入 <see cref="GatewayClient.OnEvent"/> 的 state 一致），
/// 对其 <see cref="OpenClawSignalRGatewayHubBridgeContext.UserId"/> 调用 <see cref="IHubClients.User(string)"/>。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OpenClawSignalRGatewayHubBridgeContext.UserId"/> 须与
/// <see cref="OpenClawSignalRUserIdProvider"/> / <see cref="OpenClawSignalRClaimResolution.GetUserId"/> 规则一致，否则无法命中连接。
/// </para>
/// <para>
/// 匿名或未解析出用户 id 时无法解析受众（返回 false），避免误用系统广播组或全员推送。
/// </para>
/// <para>
/// 单例 <see cref="GatewayClient"/> 仅按首个 SignalR 客户端建桥时，state 对应该用户的上下文；后续其它用户的连接不会单独收到此解析器下的网关推送。
/// </para>
/// </remarks>
public sealed class CurrentUserGatewayEventAudienceResolver : IGatewayEventAudienceResolver
{
    /// <inheritdoc />
    public bool TryResolveClients(GatewayEventAudienceResolveContext context,
        [NotNullWhen(true)] out IClientProxy? target)
    {
        target = null;
        if (context.State is not OpenClawSignalRGatewayHubBridgeContext bridge)
            return false;

        if (string.IsNullOrWhiteSpace(bridge.UserId))
            return false;

        target = context.Clients.User(bridge.UserId);
        return true;
    }
}