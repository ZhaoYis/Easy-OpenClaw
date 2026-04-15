namespace OpenClaw.Core.SignalR;

/// <summary>
/// SignalR 桥接配置：客户端事件名、RPC 白名单、JWT/Claim/分组、网关事件受众解析、系统广播。
/// </summary>
public sealed class OpenClawSignalROptions
{
    /// <summary>在 <c>appsettings.json</c> 中绑定本类型的节名。</summary>
    public const string SectionName = "OpenClawSignalR";

    /// <summary>推送给 SignalR 客户端的网关事件方法名（客户端 <c>connection.on</c>）。</summary>
    public string GatewayEventClientMethod { get; set; } = "GatewayEvent";

    /// <summary>系统级广播方法名（camelCase 便于 JS 客户端）。</summary>
    public string SystemBroadcastClientMethod { get; set; } = "systemBroadcast";

    /// <summary>用于从 query <c>access_token</c> 读取 JWT 的路径前缀（须与 <c>MapHub</c> 路径一致）。</summary>
    public string SignalRHubPathPrefix { get; set; } = "/hubs";

    /// <summary>
    /// 若非空，仅允许这些 RPC 方法经 Hub 调用；为空表示不限制（生产环境务必配置白名单）。
    /// </summary>
    public string[]? AllowedRpcMethods { get; set; }

    /// <summary>若非空，仅向客户端转发列表中的网关事件名；为空表示转发全部（仍受受众解析器约束）。</summary>
    public string[]? EventAllowlist { get; set; }

    /// <summary>
    /// 为 true 时，在<strong>首个</strong> SignalR 客户端完成建连后调用 <see cref="OpenClaw.Core.Client.GatewayClient.ConnectWithRetryAsync"/>；
    /// 最后一个客户端断开后断开网关传输。为 false 时仅注册网关事件订阅（便于测试或与假 RPC 并存），不主动建连。
    /// </summary>
    public bool EnableBackgroundConnect { get; set; }

    /// <summary>
    /// 网关事件转发模式；默认 <see cref="GatewayEventBroadcastMode.ResolverOnly"/>，须注册 <see cref="IGatewayEventAudienceResolver"/> 才会实际推送。
    /// </summary>
    public GatewayEventBroadcastMode GatewayEventBroadcastMode { get; set; } = GatewayEventBroadcastMode.ResolverOnly;

    /// <summary>受众解析失败时是否打日志（建议生产开启）。</summary>
    public bool LogUnresolvedGatewayEventAudience { get; set; } = true;

    /// <summary>Hub 要求已认证用户（与 <see cref="OpenClawGatewayHub"/> 的 <c>[Authorize]</c> 配合）。匿名场景请使用 <see cref="OpenClawGatewayHubAllowAnonymous"/>。</summary>
    public bool RequireAuthentication { get; set; } = true;

    /// <summary>用于 <see cref="Microsoft.AspNetCore.SignalR.IUserIdProvider"/> 与单用户组名的用户标识 Claim 类型。</summary>
    public string UserIdClaimType { get; set; } = "sub";

    /// <summary>身份档位 Claim 类型（如访客/付费/企业）；为 null 时不加入档位组。</summary>
    public string? TierClaimType { get; set; }

    /// <summary>单用户 SignalR 组名前缀，完整组名为前缀 + <see cref="OpenClawSignalRGroupNames.NormalizeSegment"/>（用户 id）。</summary>
    public string UserGroupPrefix { get; set; } = "oc:user:";

    /// <summary>档位组名前缀，完整组名为前缀 + 规范化后的档位 Claim 值。</summary>
    public string TierGroupPrefix { get; set; } = "oc:tier:";

    /// <summary>系统广播组；连接成功后自动加入，供 <see cref="IOpenClawSystemBroadcastSender{THub}"/> 使用。</summary>
    public string SystemBroadcastGroupName { get; set; } = "oc:system";

    /// <summary>嵌套 JWT 配置，绑定节 <c>OpenClawSignalR:Jwt</c>。</summary>
    public OpenClawSignalRJwtOptions Jwt { get; set; } = new();

    /// <summary>连接载荷缓存键前缀（内置 Memory / Hybrid 存储共用逻辑键）。</summary>
    public string ConnectionPresencePayloadKeyPrefix { get; set; } = "OpenClaw.SignalR.Presence.Payload:";

    /// <summary>
    /// 匿名或未解析出 <see cref="UserIdClaimType"/> 时，参与载荷键与索引的占位段（经 <see cref="OpenClawSignalRGroupNames.NormalizeSegment"/>）；
    /// 完整键形如 <c>前缀 + segment + ":" + connectionId</c>。
    /// </summary>
    public string ConnectionPresenceAnonymousKeySegment { get; set; } = "anon";

    /// <summary>分布式连接索引键（内置 Hybrid 存储），内容为 JSON 字符串数组，每项为 <c>userKeySegment|connectionId</c>。</summary>
    public string ConnectionPresenceIndexKey { get; set; } = "OpenClaw.SignalR.Presence.Index";
}