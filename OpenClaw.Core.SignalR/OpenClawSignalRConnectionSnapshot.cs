namespace OpenClaw.Core.SignalR;

/// <summary>
/// 单条 SignalR 连接的运营侧快照（由 <see cref="IOpenClawSignalRConnectionPresenceStore"/> 维护）。
/// </summary>
public sealed record OpenClawSignalRConnectionSnapshot(
    string ConnectionId,
    string? UserId,
    string? Tier,
    DateTimeOffset ConnectedAtUtc,
    IReadOnlyList<string> SignalRGroups,
    OpenClawSignalRPrincipalSnapshot? Principal);