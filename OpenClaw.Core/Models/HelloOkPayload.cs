using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

// ─── Top-level hello-ok ────────────────────────────────

/// <summary>
/// connect 握手成功后网关返回的 hello-ok 完整载荷，包含服务器信息、功能列表、快照等。
/// </summary>
public sealed record HelloOkPayload
{
    /// <summary>载荷类型标识（通常为 "hello-ok"）</summary>
    [JsonPropertyName("type")] public string Type { get; init; } = "";

    /// <summary>网关通信协议版本号</summary>
    [JsonPropertyName("protocol")] public int Protocol { get; init; }

    /// <summary>网关服务端信息（版本、连接 ID）</summary>
    [JsonPropertyName("server")] public ServerInfo? Server { get; init; }

    /// <summary>服务端支持的 RPC 方法与事件类型列表</summary>
    [JsonPropertyName("features")] public FeaturesInfo? Features { get; init; }

    /// <summary>网关当前状态快照（在线设备、健康状态、会话默认值等）</summary>
    [JsonPropertyName("snapshot")] public SnapshotInfo? Snapshot { get; init; }

    /// <summary>Canvas 可视化面板的宿主 URL</summary>
    [JsonPropertyName("canvasHostUrl")] public string? CanvasHostUrl { get; init; }

    /// <summary>本次连接的认证结果（角色、作用域、DeviceToken）</summary>
    [JsonPropertyName("auth")] public HelloAuthInfo? Auth { get; init; }

    /// <summary>网关通信策略（最大载荷、tick 间隔等限制）</summary>
    [JsonPropertyName("policy")] public PolicyInfo? Policy { get; init; }
}

// ─── server ────────────────────────────────────────────

/// <summary>
/// 网关服务端的基本信息。
/// </summary>
public sealed record ServerInfo
{
    /// <summary>网关服务端版本号</summary>
    [JsonPropertyName("version")] public string Version { get; init; } = "";

    /// <summary>当前 WebSocket 连接的唯一标识</summary>
    [JsonPropertyName("connId")] public string ConnId { get; init; } = "";
}

// ─── features ──────────────────────────────────────────

/// <summary>
/// 网关支持的功能清单，列出所有可调用的 RPC 方法和可订阅的事件类型。
/// </summary>
public sealed record FeaturesInfo
{
    /// <summary>服务端注册的 RPC 方法名列表</summary>
    [JsonPropertyName("methods")] public string[] Methods { get; init; } = [];

    /// <summary>服务端可推送的事件类型列表</summary>
    [JsonPropertyName("events")] public string[] Events { get; init; } = [];
}

// ─── auth ──────────────────────────────────────────────

/// <summary>
/// hello-ok 中返回的认证信息，用于确认当前连接的身份与权限。
/// </summary>
public sealed record HelloAuthInfo
{
    /// <summary>网关签发的设备令牌，后续重连时可用于免挑战认证</summary>
    [JsonPropertyName("deviceToken")] public string? DeviceToken { get; init; }

    /// <summary>当前连接被授予的角色（如 "operator"、"node"）</summary>
    [JsonPropertyName("role")] public string? Role { get; init; }

    /// <summary>当前连接被授予的权限作用域列表</summary>
    [JsonPropertyName("scopes")] public string[]? Scopes { get; init; }

    /// <summary>DeviceToken 签发时间（Unix 毫秒）</summary>
    [JsonPropertyName("issuedAtMs")] public long? IssuedAtMs { get; init; }
}

// ─── policy ────────────────────────────────────────────

/// <summary>
/// 网关通信策略限制参数。
/// </summary>
public sealed record PolicyInfo
{
    /// <summary>单条消息最大载荷字节数</summary>
    [JsonPropertyName("maxPayload")] public long MaxPayload { get; init; }

    /// <summary>服务端允许的最大缓冲字节数</summary>
    [JsonPropertyName("maxBufferedBytes")] public long MaxBufferedBytes { get; init; }

    /// <summary>服务端 tick 心跳推送间隔（毫秒）</summary>
    [JsonPropertyName("tickIntervalMs")] public int TickIntervalMs { get; init; }
}

// ─── snapshot ──────────────────────────────────────────

/// <summary>
/// 网关当前状态的完整快照，在连接建立时一次性下发。
/// </summary>
public sealed record SnapshotInfo
{
    /// <summary>当前在线设备/连接的列表</summary>
    [JsonPropertyName("presence")] public PresenceEntry[]? Presence { get; init; }

    /// <summary>网关及各渠道的健康状态</summary>
    [JsonPropertyName("health")] public HealthInfo? Health { get; init; }

    /// <summary>状态版本号，用于增量同步判定</summary>
    [JsonPropertyName("stateVersion")] public StateVersionInfo? StateVersion { get; init; }

    /// <summary>网关进程已运行时长（毫秒）</summary>
    [JsonPropertyName("uptimeMs")] public long? UptimeMs { get; init; }

    /// <summary>网关配置文件路径</summary>
    [JsonPropertyName("configPath")] public string? ConfigPath { get; init; }

    /// <summary>网关状态数据存储目录</summary>
    [JsonPropertyName("stateDir")] public string? StateDir { get; init; }

    /// <summary>会话相关默认值（默认 Agent、主会话键等）</summary>
    [JsonPropertyName("sessionDefaults")] public SessionDefaultsInfo? SessionDefaults { get; init; }

    /// <summary>当前认证模式（如 "token"、"open"）</summary>
    [JsonPropertyName("authMode")] public string? AuthMode { get; init; }

    /// <summary>可用更新信息（当有新版本时出现）</summary>
    [JsonPropertyName("updateAvailable")] public UpdateAvailableInfo? UpdateAvailable { get; init; }
}

// ─── snapshot.presence[] ───────────────────────────────

/// <summary>
/// 在线设备/连接的详细信息条目。
/// </summary>
public sealed record PresenceEntry
{
    /// <summary>设备主机名</summary>
    [JsonPropertyName("host")] public string? Host { get; init; }

    /// <summary>设备连接的 IP 地址</summary>
    [JsonPropertyName("ip")] public string? Ip { get; init; }

    /// <summary>客户端版本号</summary>
    [JsonPropertyName("version")] public string? Version { get; init; }

    /// <summary>操作系统/平台标识（如 "MacIntel"、"Win32"）</summary>
    [JsonPropertyName("platform")] public string? Platform { get; init; }

    /// <summary>设备系列（如 "desktop"、"mobile"）</summary>
    [JsonPropertyName("deviceFamily")] public string? DeviceFamily { get; init; }

    /// <summary>客户端运行模式（如 "cli"、"ui"）</summary>
    [JsonPropertyName("mode")] public string? Mode { get; init; }

    /// <summary>上线/离线原因描述</summary>
    [JsonPropertyName("reason")] public string? Reason { get; init; }

    /// <summary>附加文本信息</summary>
    [JsonPropertyName("text")] public string? Text { get; init; }

    /// <summary>状态更新时间戳（Unix 毫秒）</summary>
    [JsonPropertyName("ts")] public long? Ts { get; init; }

    /// <summary>设备被授予的角色列表</summary>
    [JsonPropertyName("roles")] public string[]? Roles { get; init; }

    /// <summary>设备被授予的权限作用域列表</summary>
    [JsonPropertyName("scopes")] public string[]? Scopes { get; init; }

    /// <summary>设备唯一标识（SHA-256 公钥哈希）</summary>
    [JsonPropertyName("deviceId")] public string? DeviceId { get; init; }

    /// <summary>当前连接实例的唯一 ID（同一设备可多次连接）</summary>
    [JsonPropertyName("instanceId")] public string? InstanceId { get; init; }
}

// ─── snapshot.health ───────────────────────────────────

/// <summary>
/// 网关整体健康状态，包括各渠道、Agent 和会话的运行情况。
/// </summary>
public sealed record HealthInfo
{
    /// <summary>网关整体是否健康</summary>
    [JsonPropertyName("ok")] public bool Ok { get; init; }

    /// <summary>最近一次健康检查的时间戳（Unix 毫秒）</summary>
    [JsonPropertyName("ts")] public long Ts { get; init; }

    /// <summary>健康检查耗时（毫秒）</summary>
    [JsonPropertyName("durationMs")] public int DurationMs { get; init; }

    /// <summary>各渠道的健康状态，键为渠道名称</summary>
    [JsonPropertyName("channels")] public Dictionary<string, ChannelHealth>? Channels { get; init; }

    /// <summary>渠道在 UI 中的显示顺序</summary>
    [JsonPropertyName("channelOrder")] public string[]? ChannelOrder { get; init; }

    /// <summary>渠道的人类可读标签，键为渠道名称，值为显示名</summary>
    [JsonPropertyName("channelLabels")] public Dictionary<string, string>? ChannelLabels { get; init; }

    /// <summary>全局心跳间隔（秒）</summary>
    [JsonPropertyName("heartbeatSeconds")] public int? HeartbeatSeconds { get; init; }

    /// <summary>默认 Agent 的标识符</summary>
    [JsonPropertyName("defaultAgentId")] public string? DefaultAgentId { get; init; }

    /// <summary>各 Agent 的健康状态列表</summary>
    [JsonPropertyName("agents")] public AgentHealth[]? Agents { get; init; }

    /// <summary>全局会话摘要信息</summary>
    [JsonPropertyName("sessions")] public SessionsSummary? Sessions { get; init; }
}

/// <summary>
/// 单个渠道（如 Telegram、Discord、Web 等）的健康状态。
/// </summary>
public sealed record ChannelHealth
{
    /// <summary>渠道是否已配置</summary>
    [JsonPropertyName("configured")] public bool Configured { get; init; }

    /// <summary>渠道是否正在运行</summary>
    [JsonPropertyName("running")] public bool Running { get; init; }

    /// <summary>最近一次启动时间（Unix 毫秒）</summary>
    [JsonPropertyName("lastStartAt")] public long? LastStartAt { get; init; }

    /// <summary>最近一次停止时间（Unix 毫秒）</summary>
    [JsonPropertyName("lastStopAt")] public long? LastStopAt { get; init; }

    /// <summary>最近一次错误信息</summary>
    [JsonPropertyName("lastError")] public string? LastError { get; init; }

    /// <summary>渠道监听的端口号（适用于 HTTP 类渠道）</summary>
    [JsonPropertyName("port")] public int? Port { get; init; }

    /// <summary>最近一次存活探针检查时间（Unix 毫秒）</summary>
    [JsonPropertyName("lastProbeAt")] public long? LastProbeAt { get; init; }

    /// <summary>渠道绑定的账户 ID</summary>
    [JsonPropertyName("accountId")] public string? AccountId { get; init; }

    /// <summary>多账户渠道下各子账户的健康状态</summary>
    [JsonPropertyName("accounts")] public Dictionary<string, ChannelHealth>? Accounts { get; init; }
}

// ─── snapshot.health.agents[] ──────────────────────────

/// <summary>
/// 单个 Agent 的健康状态信息。
/// </summary>
public sealed record AgentHealth
{
    /// <summary>Agent 唯一标识符</summary>
    [JsonPropertyName("agentId")] public string AgentId { get; init; } = "";

    /// <summary>是否为系统默认 Agent</summary>
    [JsonPropertyName("isDefault")] public bool IsDefault { get; init; }

    /// <summary>该 Agent 的心跳配置</summary>
    [JsonPropertyName("heartbeat")] public HeartbeatConfig? Heartbeat { get; init; }

    /// <summary>该 Agent 拥有的会话摘要</summary>
    [JsonPropertyName("sessions")] public SessionsSummary? Sessions { get; init; }
}

/// <summary>
/// Agent 的定时心跳配置，用于周期性触发 Agent 执行任务。
/// </summary>
public sealed record HeartbeatConfig
{
    /// <summary>心跳是否启用</summary>
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }

    /// <summary>心跳间隔的人类可读表示（如 "30s"、"5m"）</summary>
    [JsonPropertyName("every")] public string? Every { get; init; }

    /// <summary>心跳间隔（毫秒）</summary>
    [JsonPropertyName("everyMs")] public long? EveryMs { get; init; }

    /// <summary>心跳触发时发送给 Agent 的提示词</summary>
    [JsonPropertyName("prompt")] public string? Prompt { get; init; }

    /// <summary>心跳消息投递的目标会话键</summary>
    [JsonPropertyName("target")] public string? Target { get; init; }

    /// <summary>心跳确认回复的最大字符数限制</summary>
    [JsonPropertyName("ackMaxChars")] public int? AckMaxChars { get; init; }
}

// ─── snapshot.health.sessions / agents[].sessions ──────

/// <summary>
/// 会话数量与最近活跃会话的摘要信息。
/// </summary>
public sealed record SessionsSummary
{
    /// <summary>会话存储目录路径</summary>
    [JsonPropertyName("path")] public string? Path { get; init; }

    /// <summary>会话总数</summary>
    [JsonPropertyName("count")] public int Count { get; init; }

    /// <summary>最近活跃的会话列表</summary>
    [JsonPropertyName("recent")] public RecentSession[]? Recent { get; init; }
}

/// <summary>
/// 最近活跃的单个会话信息。
/// </summary>
public sealed record RecentSession
{
    /// <summary>会话唯一键（如 "agent:main:main"）</summary>
    [JsonPropertyName("key")] public string Key { get; init; } = "";

    /// <summary>最后更新时间戳（Unix 毫秒）</summary>
    [JsonPropertyName("updatedAt")] public long UpdatedAt { get; init; }

    /// <summary>距今时间（毫秒）</summary>
    [JsonPropertyName("age")] public long Age { get; init; }
}

// ─── snapshot.stateVersion ─────────────────────────────

/// <summary>
/// 状态版本号，客户端可据此判断是否需要重新拉取 presence 或 health。
/// </summary>
public sealed record StateVersionInfo
{
    /// <summary>在线设备列表的版本号（每次 presence 变更递增）</summary>
    [JsonPropertyName("presence")] public long Presence { get; init; }

    /// <summary>健康状态的版本号（每次 health 变更递增）</summary>
    [JsonPropertyName("health")] public long Health { get; init; }
}

// ─── snapshot.sessionDefaults ──────────────────────────

/// <summary>
/// 网关的会话默认值配置。
/// </summary>
public sealed record SessionDefaultsInfo
{
    /// <summary>默认 Agent 标识符</summary>
    [JsonPropertyName("defaultAgentId")] public string DefaultAgentId { get; init; } = "";

    /// <summary>主会话的简短键名</summary>
    [JsonPropertyName("mainKey")] public string MainKey { get; init; } = "";

    /// <summary>主会话的完整会话键（格式：agent:scope:key）</summary>
    [JsonPropertyName("mainSessionKey")] public string MainSessionKey { get; init; } = "";

    /// <summary>会话作用域</summary>
    [JsonPropertyName("scope")] public string Scope { get; init; } = "";
}

// ─── snapshot.updateAvailable ──────────────────────────

/// <summary>
/// 网关可用更新信息。
/// </summary>
public sealed record UpdateAvailableInfo
{
    /// <summary>当前安装的版本号</summary>
    [JsonPropertyName("currentVersion")] public string CurrentVersion { get; init; } = "";

    /// <summary>最新可用的版本号</summary>
    [JsonPropertyName("latestVersion")] public string LatestVersion { get; init; } = "";

    /// <summary>更新渠道（如 "stable"、"beta"）</summary>
    [JsonPropertyName("channel")] public string Channel { get; init; } = "";
}
