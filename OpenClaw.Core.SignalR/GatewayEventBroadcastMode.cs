namespace OpenClaw.Core.SignalR;

/// <summary>
/// 网关事件向 SignalR 客户端的转发策略。
/// </summary>
public enum GatewayEventBroadcastMode
{
    /// <summary>不向客户端转发网关事件（仍可注册处理器做日志等）。</summary>
    None,

    /// <summary>仅通过 <see cref="IGatewayEventAudienceResolver"/> 解析目标；解析失败则不发送（默认，防串消息）。</summary>
    ResolverOnly,
}
