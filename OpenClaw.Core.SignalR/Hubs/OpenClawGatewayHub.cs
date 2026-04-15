using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 默认网关 Hub，要求 JWT 认证；映射后请使用 <c>RequireAuthorization()</c> 或与该属性一致的全局策略。
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class OpenClawGatewayHub : OpenClawGatewayHubBase
{
    /// <summary>
    /// 构造需 JWT 的 Hub；参数转发至基类以注入 RPC、选项、在线存储与桥接。
    /// </summary>
    public OpenClawGatewayHub(
        IOpenClawGatewayRpc rpc,
        IOptions<OpenClawSignalROptions> options,
        IOpenClawSignalRConnectionPresenceStore presenceStore,
        IOpenClawSignalRGatewayHubBridge hubBridge,
        ILoggerFactory loggerFactory)
        : base(rpc, options, presenceStore, hubBridge, loggerFactory)
    {
    }
}
