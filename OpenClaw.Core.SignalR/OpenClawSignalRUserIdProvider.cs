using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 从 <see cref="OpenClawSignalROptions.UserIdClaimType"/> 读取 SignalR 用户标识，供 <see cref="IHubClients.User"/> 使用。
/// </summary>
public sealed class OpenClawSignalRUserIdProvider : IUserIdProvider
{
    private readonly IOptionsMonitor<OpenClawSignalROptions> _options;

    public OpenClawSignalRUserIdProvider(IOptionsMonitor<OpenClawSignalROptions> options)
    {
        _options = options;
    }

    public string? GetUserId(HubConnectionContext connection) =>
        OpenClawSignalRClaimResolution.GetUserId(connection.User, _options.CurrentValue.UserIdClaimType);
}
