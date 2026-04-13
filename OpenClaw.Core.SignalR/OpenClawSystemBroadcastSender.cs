using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 通过 <c>Clients.Group(SystemBroadcastGroupName)</c> 发送系统广播。
/// </summary>
public sealed class OpenClawSystemBroadcastSender<THub> : IOpenClawSystemBroadcastSender<THub>
    where THub : Hub
{
    private readonly IHubContext<THub> _hubContext;
    private readonly IOptions<OpenClawSignalROptions> _options;

    public OpenClawSystemBroadcastSender(IHubContext<THub> hubContext, IOptions<OpenClawSignalROptions> options)
    {
        _hubContext = hubContext;
        _options = options;
    }

    public Task SendAsync(object? payload, CancellationToken cancellationToken = default)
    {
        var o = _options.Value;
        return _hubContext.Clients.Group(o.SystemBroadcastGroupName)
            .SendAsync(o.SystemBroadcastClientMethod, payload, cancellationToken);
    }
}
