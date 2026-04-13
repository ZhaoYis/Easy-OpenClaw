using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 不要求认证的网关 Hub，用于开发或内网；生产环境优先使用 <see cref="OpenClawGatewayHub"/>。
/// 匿名连接不会加入用户/档位/系统广播组（与 JWT 场景隔离）。
/// </summary>
[AllowAnonymous]
public sealed class OpenClawGatewayHubAllowAnonymous : OpenClawGatewayHubBase
{
    public OpenClawGatewayHubAllowAnonymous(
        IOpenClawGatewayRpc rpc,
        IOptions<OpenClawSignalROptions> options,
        IOpenClawSignalRConnectionPresenceStore presenceStore,
        ILoggerFactory loggerFactory)
        : base(rpc, options, presenceStore, loggerFactory)
    {
    }
}
