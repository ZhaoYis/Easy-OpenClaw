using Microsoft.AspNetCore.SignalR;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 将单条网关事件与 SignalR 客户端集合、选项及（可选）连接快照打包，供 <see cref="IGatewayEventAudienceResolver"/> 解析 <see cref="IClientProxy"/>。
/// </summary>
/// <param name="Event">来自网关的推送事件</param>
/// <param name="Clients">当前 Hub 的 <see cref="IHubClients"/>（全部/组/用户/多连接）</param>
/// <param name="Options">桥接与分组相关配置</param>
/// <param name="ConnectionSnapshots">当解析器 <see cref="IGatewayEventAudienceResolver.RequiresConnectionSnapshotEnumeration"/> 为 true 时由协调器预取</param>
/// <param name="State">订阅方自定义上下文</param>
public sealed record GatewayEventAudienceResolveContext(
    GatewayEvent Event,
    IHubClients Clients,
    OpenClawSignalROptions Options,
    IReadOnlyList<OpenClawSignalRConnectionSnapshot>? ConnectionSnapshots,
    object? State);