namespace OpenClaw.Core.SignalR;

/// <summary>
/// 单条 SignalR 连接的运营侧快照（由 <see cref="IOpenClawSignalRConnectionPresenceStore"/> 维护）。
/// </summary>
/// <param name="ConnectionId">连接 id</param>
/// <param name="UserId">与 <see cref="OpenClawSignalROptions.UserIdClaimType"/> 解析结果一致；匿名时为 null</param>
/// <param name="Tier">档位 Claim；未配置或未解析时为 null</param>
/// <param name="ConnectedAtUtc">注册快照时的 UTC 时间</param>
/// <param name="SignalRGroups">该连接已加入的组名（与 Hub 逻辑一致）</param>
/// <param name="Principal">可序列化身份快照，便于审计或跨节点还原</param>
public sealed record OpenClawSignalRConnectionSnapshot(
    string ConnectionId,
    string? UserId,
    string? Tier,
    DateTimeOffset ConnectedAtUtc,
    IReadOnlyList<string> SignalRGroups,
    OpenClawSignalRPrincipalSnapshot? Principal);