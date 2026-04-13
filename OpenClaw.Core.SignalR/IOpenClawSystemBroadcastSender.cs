using Microsoft.AspNetCore.SignalR;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 向已加入 <see cref="OpenClawSignalROptions.SystemBroadcastGroupName"/> 的在线连接发送系统级通知（与网关事件通道分离）。
/// </summary>
public interface IOpenClawSystemBroadcastSender<THub>
    where THub : Hub
{
    /// <summary>使用 <see cref="OpenClawSignalROptions.SystemBroadcastClientMethod"/> 推送载荷。</summary>
    Task SendAsync(object? payload, CancellationToken cancellationToken = default);
}
