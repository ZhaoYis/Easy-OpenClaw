using Microsoft.AspNetCore.SignalR;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 向已加入 <see cref="OpenClawSignalROptions.SystemBroadcastGroupName"/> 的在线连接发送系统级通知（与网关事件通道分离）。
/// </summary>
public interface IOpenClawSystemBroadcastSender<THub>
    where THub : Hub
{
    /// <summary>向 <see cref="OpenClawSignalROptions.SystemBroadcastGroupName"/> 组调用 <see cref="OpenClawSignalROptions.SystemBroadcastClientMethod"/>。</summary>
    /// <param name="payload">传给客户端方法的单个参数，可为 null</param>
    /// <param name="cancellationToken">取消发送</param>
    Task SendAsync(object? payload, CancellationToken cancellationToken = default);
}
