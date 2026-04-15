using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 从 <see cref="OpenClawSignalROptions.UserIdClaimType"/> 读取 SignalR 用户标识，供 <see cref="IHubClients.User"/> 使用。
/// </summary>
public sealed class OpenClawSignalRUserIdProvider : IUserIdProvider
{
    private readonly IOptionsMonitor<OpenClawSignalROptions> _options;

    /// <summary>注入可刷新的 SignalR 选项（读取 <see cref="OpenClawSignalROptions.UserIdClaimType"/>）。</summary>
    public OpenClawSignalRUserIdProvider(IOptionsMonitor<OpenClawSignalROptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// 返回当前 SignalR 连接的用户标识，供 <c>Clients.User(userId)</c> 路由；规则与 <see cref="OpenClawSignalRClaimResolution.GetUserId"/> 一致。
    /// </summary>
    /// <param name="connection">Hub 连接上下文</param>
    /// <returns>非空白用户 id；无法从 Claims 解析时返回 null</returns>
    public string? GetUserId(HubConnectionContext connection) =>
        OpenClawSignalRClaimResolution.GetUserId(connection.User, _options.CurrentValue.UserIdClaimType);
}
