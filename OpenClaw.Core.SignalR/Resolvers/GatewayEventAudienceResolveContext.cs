using Microsoft.AspNetCore.SignalR;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 将单条网关事件与 SignalR 客户端集合、选项及（可选）连接快照打包，供 <see cref="IGatewayEventAudienceResolver"/> 解析 <see cref="IClientProxy"/>。
/// </summary>
public sealed record GatewayEventAudienceResolveContext(
    GatewayEvent Event,
    IHubClients Clients,
    OpenClawSignalROptions Options,
    IReadOnlyList<OpenClawSignalRConnectionSnapshot>? ConnectionSnapshots);
