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

    /// <summary>注入 Hub 上下文与选项（读取系统广播组名与客户端方法名）。</summary>
    public OpenClawSystemBroadcastSender(IHubContext<THub> hubContext, IOptions<OpenClawSignalROptions> options)
    {
        _hubContext = hubContext;
        _options = options;
    }

    /// <summary>向 <see cref="OpenClawSignalROptions.SystemBroadcastGroupName"/> 组调用 <see cref="OpenClawSignalROptions.SystemBroadcastClientMethod"/>。</summary>
    /// <param name="payload">传给客户端方法的单个参数，可为 null</param>
    /// <param name="cancellationToken">取消发送</param>
    public Task SendAsync(object? payload, CancellationToken cancellationToken = default)
    {
        var o = _options.Value;
        return _hubContext.Clients.Group(o.SystemBroadcastGroupName)
            .SendAsync(o.SystemBroadcastClientMethod, payload, cancellationToken);
    }
}
