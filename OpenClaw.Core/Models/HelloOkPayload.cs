using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

// ─── Top-level hello-ok ────────────────────────────────

public sealed record HelloOkPayload
{
    [JsonPropertyName("type")] public string Type { get; init; } = "";
    [JsonPropertyName("protocol")] public int Protocol { get; init; }
    [JsonPropertyName("server")] public ServerInfo? Server { get; init; }
    [JsonPropertyName("features")] public FeaturesInfo? Features { get; init; }
    [JsonPropertyName("snapshot")] public SnapshotInfo? Snapshot { get; init; }
    [JsonPropertyName("canvasHostUrl")] public string? CanvasHostUrl { get; init; }
    [JsonPropertyName("auth")] public HelloAuthInfo? Auth { get; init; }
    [JsonPropertyName("policy")] public PolicyInfo? Policy { get; init; }
}

// ─── server ────────────────────────────────────────────

public sealed record ServerInfo
{
    [JsonPropertyName("version")] public string Version { get; init; } = "";
    [JsonPropertyName("connId")] public string ConnId { get; init; } = "";
}

// ─── features ──────────────────────────────────────────

public sealed record FeaturesInfo
{
    [JsonPropertyName("methods")] public string[] Methods { get; init; } = [];
    [JsonPropertyName("events")] public string[] Events { get; init; } = [];
}

// ─── auth ──────────────────────────────────────────────

public sealed record HelloAuthInfo
{
    [JsonPropertyName("deviceToken")] public string? DeviceToken { get; init; }
    [JsonPropertyName("role")] public string? Role { get; init; }
    [JsonPropertyName("scopes")] public string[]? Scopes { get; init; }
    [JsonPropertyName("issuedAtMs")] public long? IssuedAtMs { get; init; }
}

// ─── policy ────────────────────────────────────────────

public sealed record PolicyInfo
{
    [JsonPropertyName("maxPayload")] public long MaxPayload { get; init; }
    [JsonPropertyName("maxBufferedBytes")] public long MaxBufferedBytes { get; init; }
    [JsonPropertyName("tickIntervalMs")] public int TickIntervalMs { get; init; }
}

// ─── snapshot ──────────────────────────────────────────

public sealed record SnapshotInfo
{
    [JsonPropertyName("presence")] public PresenceEntry[]? Presence { get; init; }
    [JsonPropertyName("health")] public HealthInfo? Health { get; init; }
    [JsonPropertyName("stateVersion")] public StateVersionInfo? StateVersion { get; init; }
    [JsonPropertyName("uptimeMs")] public long? UptimeMs { get; init; }
    [JsonPropertyName("configPath")] public string? ConfigPath { get; init; }
    [JsonPropertyName("stateDir")] public string? StateDir { get; init; }
    [JsonPropertyName("sessionDefaults")] public SessionDefaultsInfo? SessionDefaults { get; init; }
    [JsonPropertyName("authMode")] public string? AuthMode { get; init; }
    [JsonPropertyName("updateAvailable")] public UpdateAvailableInfo? UpdateAvailable { get; init; }
}

// ─── snapshot.presence[] ───────────────────────────────

public sealed record PresenceEntry
{
    [JsonPropertyName("host")] public string? Host { get; init; }
    [JsonPropertyName("ip")] public string? Ip { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("platform")] public string? Platform { get; init; }
    [JsonPropertyName("deviceFamily")] public string? DeviceFamily { get; init; }
    [JsonPropertyName("mode")] public string? Mode { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
    [JsonPropertyName("text")] public string? Text { get; init; }
    [JsonPropertyName("ts")] public long? Ts { get; init; }
    [JsonPropertyName("roles")] public string[]? Roles { get; init; }
    [JsonPropertyName("scopes")] public string[]? Scopes { get; init; }
    [JsonPropertyName("deviceId")] public string? DeviceId { get; init; }
    [JsonPropertyName("instanceId")] public string? InstanceId { get; init; }
}

// ─── snapshot.health ───────────────────────────────────

public sealed record HealthInfo
{
    [JsonPropertyName("ok")] public bool Ok { get; init; }
    [JsonPropertyName("ts")] public long Ts { get; init; }
    [JsonPropertyName("durationMs")] public int DurationMs { get; init; }
    [JsonPropertyName("channels")] public Dictionary<string, ChannelHealth>? Channels { get; init; }
    [JsonPropertyName("channelOrder")] public string[]? ChannelOrder { get; init; }
    [JsonPropertyName("channelLabels")] public Dictionary<string, string>? ChannelLabels { get; init; }
    [JsonPropertyName("heartbeatSeconds")] public int? HeartbeatSeconds { get; init; }
    [JsonPropertyName("defaultAgentId")] public string? DefaultAgentId { get; init; }
    [JsonPropertyName("agents")] public AgentHealth[]? Agents { get; init; }
    [JsonPropertyName("sessions")] public SessionsSummary? Sessions { get; init; }
}

public sealed record ChannelHealth
{
    [JsonPropertyName("configured")] public bool Configured { get; init; }
    [JsonPropertyName("running")] public bool Running { get; init; }
    [JsonPropertyName("lastStartAt")] public long? LastStartAt { get; init; }
    [JsonPropertyName("lastStopAt")] public long? LastStopAt { get; init; }
    [JsonPropertyName("lastError")] public string? LastError { get; init; }
    [JsonPropertyName("port")] public int? Port { get; init; }
    [JsonPropertyName("lastProbeAt")] public long? LastProbeAt { get; init; }
    [JsonPropertyName("accountId")] public string? AccountId { get; init; }
    [JsonPropertyName("accounts")] public Dictionary<string, ChannelHealth>? Accounts { get; init; }
}

// ─── snapshot.health.agents[] ──────────────────────────

public sealed record AgentHealth
{
    [JsonPropertyName("agentId")] public string AgentId { get; init; } = "";
    [JsonPropertyName("isDefault")] public bool IsDefault { get; init; }
    [JsonPropertyName("heartbeat")] public HeartbeatConfig? Heartbeat { get; init; }
    [JsonPropertyName("sessions")] public SessionsSummary? Sessions { get; init; }
}

public sealed record HeartbeatConfig
{
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("every")] public string? Every { get; init; }
    [JsonPropertyName("everyMs")] public long? EveryMs { get; init; }
    [JsonPropertyName("prompt")] public string? Prompt { get; init; }
    [JsonPropertyName("target")] public string? Target { get; init; }
    [JsonPropertyName("ackMaxChars")] public int? AckMaxChars { get; init; }
}

// ─── snapshot.health.sessions / agents[].sessions ──────

public sealed record SessionsSummary
{
    [JsonPropertyName("path")] public string? Path { get; init; }
    [JsonPropertyName("count")] public int Count { get; init; }
    [JsonPropertyName("recent")] public RecentSession[]? Recent { get; init; }
}

public sealed record RecentSession
{
    [JsonPropertyName("key")] public string Key { get; init; } = "";
    [JsonPropertyName("updatedAt")] public long UpdatedAt { get; init; }
    [JsonPropertyName("age")] public long Age { get; init; }
}

// ─── snapshot.stateVersion ─────────────────────────────

public sealed record StateVersionInfo
{
    [JsonPropertyName("presence")] public long Presence { get; init; }
    [JsonPropertyName("health")] public long Health { get; init; }
}

// ─── snapshot.sessionDefaults ──────────────────────────

public sealed record SessionDefaultsInfo
{
    [JsonPropertyName("defaultAgentId")] public string DefaultAgentId { get; init; } = "";
    [JsonPropertyName("mainKey")] public string MainKey { get; init; } = "";
    [JsonPropertyName("mainSessionKey")] public string MainSessionKey { get; init; } = "";
    [JsonPropertyName("scope")] public string Scope { get; init; } = "";
}

// ─── snapshot.updateAvailable ──────────────────────────

public sealed record UpdateAvailableInfo
{
    [JsonPropertyName("currentVersion")] public string CurrentVersion { get; init; } = "";
    [JsonPropertyName("latestVersion")] public string LatestVersion { get; init; } = "";
    [JsonPropertyName("channel")] public string Channel { get; init; } = "";
}
