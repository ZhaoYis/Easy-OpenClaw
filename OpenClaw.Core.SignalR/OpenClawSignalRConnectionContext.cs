using System.Security.Claims;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 连接建立时供宿主扩展自定义 SignalR 组的上下文。
/// </summary>
public sealed class OpenClawSignalRConnectionContext
{
    public OpenClawSignalRConnectionContext(
        string connectionId,
        ClaimsPrincipal? user,
        OpenClawSignalROptions options)
    {
        ConnectionId = connectionId;
        User = user;
        Options = options;
    }

    public string ConnectionId { get; }

    public ClaimsPrincipal? User { get; }

    public OpenClawSignalROptions Options { get; }
}
