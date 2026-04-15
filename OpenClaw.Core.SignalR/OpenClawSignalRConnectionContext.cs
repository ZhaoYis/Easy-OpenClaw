using System.Security.Claims;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 连接建立时供宿主扩展自定义 SignalR 组的上下文。
/// </summary>
public sealed class OpenClawSignalRConnectionContext
{
    /// <summary>绑定当前连接 id、用户与只读选项副本。</summary>
    public OpenClawSignalRConnectionContext(
        string connectionId,
        ClaimsPrincipal? user,
        OpenClawSignalROptions options)
    {
        ConnectionId = connectionId;
        User = user;
        Options = options;
    }

    /// <summary>SignalR 连接标识符。</summary>
    public string ConnectionId { get; }

    /// <summary>Hub 调用方用户；匿名 Hub 可能未认证。</summary>
    public ClaimsPrincipal? User { get; }

    /// <summary>当前桥接配置（分组前缀、Claim 类型等）。</summary>
    public OpenClawSignalROptions Options { get; }
}
