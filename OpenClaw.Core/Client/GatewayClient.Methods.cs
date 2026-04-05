using System.Text.Json;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// GatewayClient 的 RPC 方法封装（partial）。
/// 每个方法对应网关服务端注册的一个 RPC 方法名，调用后自动在控制台打印关键响应信息。
/// </summary>
public sealed partial class GatewayClient
{
    /// <summary>
    /// 内部统一调用入口。发送 RPC 请求并在控制台打印结果摘要。
    /// </summary>
    private async Task<GatewayResponse> InvokeAsync(string method, object? parameters = null, CancellationToken ct = default)
    {
        var resp = parameters is null
            ? await SendRequestRawAsync(method, JsonSerializer.SerializeToElement(new { }), ct)
            : await SendRequestAsync(method, parameters, ct);

        if (resp.Ok)
        {
            var preview = resp.Payload?.GetRawText() is { } raw ? Truncate(raw, 200) : "(empty)";
            Log.Success($"[{method}] → {preview}");
        }
        else
        {
            var err = resp.Error?.GetRawText() ?? "unknown";
            Log.Error($"[{method}] 失败 → {err}");
        }

        return resp;
    }

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "…";

    // ═══════════════════════════════════════════════════════════
    //  Health & Status
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> HealthAsync(CancellationToken ct = default)
        => InvokeAsync("health", ct: ct);

    public Task<GatewayResponse> DoctorMemoryStatusAsync(CancellationToken ct = default)
        => InvokeAsync("doctor.memory.status", ct: ct);

    public Task<GatewayResponse> LogsTailAsync(int lines = 50, CancellationToken ct = default)
        => InvokeAsync("logs.tail", new { lines }, ct);

    public Task<GatewayResponse> ChannelsStatusAsync(CancellationToken ct = default)
        => InvokeAsync("channels.status", ct: ct);

    public Task<GatewayResponse> ChannelsLogoutAsync(string channel, CancellationToken ct = default)
        => InvokeAsync("channels.logout", new { channel }, ct);

    public Task<GatewayResponse> StatusAsync(CancellationToken ct = default)
        => InvokeAsync("status", ct: ct);

    public Task<GatewayResponse> UsageStatusAsync(CancellationToken ct = default)
        => InvokeAsync("usage.status", ct: ct);

    public Task<GatewayResponse> UsageCostAsync(CancellationToken ct = default)
        => InvokeAsync("usage.cost", ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  TTS
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> TtsStatusAsync(CancellationToken ct = default)
        => InvokeAsync("tts.status", ct: ct);

    public Task<GatewayResponse> TtsProvidersAsync(CancellationToken ct = default)
        => InvokeAsync("tts.providers", ct: ct);

    public Task<GatewayResponse> TtsEnableAsync(CancellationToken ct = default)
        => InvokeAsync("tts.enable", ct: ct);

    public Task<GatewayResponse> TtsDisableAsync(CancellationToken ct = default)
        => InvokeAsync("tts.disable", ct: ct);

    public Task<GatewayResponse> TtsConvertAsync(string text, string? voice = null, CancellationToken ct = default)
        => InvokeAsync("tts.convert", new { text, voice }, ct);

    public Task<GatewayResponse> TtsSetProviderAsync(string provider, CancellationToken ct = default)
        => InvokeAsync("tts.setProvider", new { provider }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Config
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> ConfigGetAsync(string? key = null, CancellationToken ct = default)
        => InvokeAsync("config.get", new { key }, ct);

    public Task<GatewayResponse> ConfigSetAsync(string key, object value, CancellationToken ct = default)
        => InvokeAsync("config.set", new { key, value }, ct);

    public Task<GatewayResponse> ConfigApplyAsync(CancellationToken ct = default)
        => InvokeAsync("config.apply", ct: ct);

    public Task<GatewayResponse> ConfigPatchAsync(object patch, CancellationToken ct = default)
        => InvokeAsync("config.patch", new { patch }, ct);

    public Task<GatewayResponse> ConfigSchemaAsync(CancellationToken ct = default)
        => InvokeAsync("config.schema", ct: ct);

    public Task<GatewayResponse> ConfigSchemaLookupAsync(string path, CancellationToken ct = default)
        => InvokeAsync("config.schema.lookup", new { path }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Exec Approvals
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> ExecApprovalsGetAsync(CancellationToken ct = default)
        => InvokeAsync("exec.approvals.get", ct: ct);

    public Task<GatewayResponse> ExecApprovalsSetAsync(object approvals, CancellationToken ct = default)
        => InvokeAsync("exec.approvals.set", approvals, ct);

    public Task<GatewayResponse> ExecApprovalsNodeGetAsync(string? nodeId = null, CancellationToken ct = default)
        => InvokeAsync("exec.approvals.node.get", new { nodeId }, ct);

    public Task<GatewayResponse> ExecApprovalsNodeSetAsync(string nodeId, object approvals, CancellationToken ct = default)
        => InvokeAsync("exec.approvals.node.set", new { nodeId, approvals }, ct);

    public Task<GatewayResponse> ExecApprovalRequestAsync(object request, CancellationToken ct = default)
        => InvokeAsync("exec.approval.request", request, ct);

    public Task<GatewayResponse> ExecApprovalWaitDecisionAsync(string approvalId, CancellationToken ct = default)
        => InvokeAsync("exec.approval.waitDecision", new { approvalId }, ct);

    public Task<GatewayResponse> ExecApprovalResolveAsync(string approvalId, string decision, CancellationToken ct = default)
        => InvokeAsync("exec.approval.resolve", new { approvalId, decision }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Wizard
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> WizardStartAsync(string sessionId, string? wizardType = null, CancellationToken ct = default)
        => InvokeAsync("wizard.start", new { sessionId, wizardType }, ct);

    public Task<GatewayResponse> WizardNextAsync(string sessionId, object? input = null, CancellationToken ct = default)
        => InvokeAsync("wizard.next", new { sessionId, input }, ct);

    public Task<GatewayResponse> WizardCancelAsync(string sessionId, CancellationToken ct = default)
        => InvokeAsync("wizard.cancel", new { sessionId }, ct);

    public Task<GatewayResponse> WizardStatusAsync(string sessionId, CancellationToken ct = default)
        => InvokeAsync("wizard.status", new { sessionId }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Talk
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> TalkConfigAsync(CancellationToken ct = default)
        => InvokeAsync("talk.config", ct: ct);

    public Task<GatewayResponse> TalkModeAsync(string? mode = null, CancellationToken ct = default)
        => InvokeAsync("talk.mode", new { mode }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Models & Tools
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> ModelsListAsync(CancellationToken ct = default)
        => InvokeAsync("models.list", ct: ct);

    public Task<GatewayResponse> ToolsCatalogAsync(CancellationToken ct = default)
        => InvokeAsync("tools.catalog", ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  Agents
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> AgentsListAsync(CancellationToken ct = default)
        => InvokeAsync("agents.list", ct: ct);

    public Task<GatewayResponse> AgentsCreateAsync(object agentConfig, CancellationToken ct = default)
        => InvokeAsync("agents.create", agentConfig, ct);

    public Task<GatewayResponse> AgentsUpdateAsync(string agentId, object updates, CancellationToken ct = default)
        => InvokeAsync("agents.update", new { agentId, updates }, ct);

    public Task<GatewayResponse> AgentsDeleteAsync(string agentId, CancellationToken ct = default)
        => InvokeAsync("agents.delete", new { agentId }, ct);

    public Task<GatewayResponse> AgentsFilesListAsync(string agentId, CancellationToken ct = default)
        => InvokeAsync("agents.files.list", new { agentId }, ct);

    public Task<GatewayResponse> AgentsFilesGetAsync(string agentId, string fileName, CancellationToken ct = default)
        => InvokeAsync("agents.files.get", new { agentId, fileName }, ct);

    public Task<GatewayResponse> AgentsFilesSetAsync(string agentId, string fileName, string content, CancellationToken ct = default)
        => InvokeAsync("agents.files.set", new { agentId, fileName, content }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Skills
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> SkillsStatusAsync(CancellationToken ct = default)
        => InvokeAsync("skills.status", ct: ct);

    public Task<GatewayResponse> SkillsBinsAsync(CancellationToken ct = default)
        => InvokeAsync("skills.bins", ct: ct);

    public Task<GatewayResponse> SkillsInstallAsync(string skillId, CancellationToken ct = default)
        => InvokeAsync("skills.install", new { skillId }, ct);

    public Task<GatewayResponse> SkillsUpdateAsync(string? skillId = null, CancellationToken ct = default)
        => InvokeAsync("skills.update", new { skillId }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Update
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> UpdateRunAsync(CancellationToken ct = default)
        => InvokeAsync("update.run", ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  Voice Wake
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> VoicewakeGetAsync(CancellationToken ct = default)
        => InvokeAsync("voicewake.get", ct: ct);

    public Task<GatewayResponse> VoicewakeSetAsync(object config, CancellationToken ct = default)
        => InvokeAsync("voicewake.set", config, ct);

    // ═══════════════════════════════════════════════════════════
    //  Secrets
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> SecretsReloadAsync(CancellationToken ct = default)
        => InvokeAsync("secrets.reload", ct: ct);

    public Task<GatewayResponse> SecretsResolveAsync(string key, CancellationToken ct = default)
        => InvokeAsync("secrets.resolve", new { key }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Sessions
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> SessionsListAsync(CancellationToken ct = default)
        => InvokeAsync("sessions.list", ct: ct);

    public Task<GatewayResponse> SessionsPreviewAsync(string sessionKey, CancellationToken ct = default)
        => InvokeAsync("sessions.preview", new { sessionKey }, ct);

    public Task<GatewayResponse> SessionsPatchAsync(string sessionKey, object patch, CancellationToken ct = default)
        => InvokeAsync("sessions.patch", new { sessionKey, patch }, ct);

    public Task<GatewayResponse> SessionsResetAsync(string sessionKey, CancellationToken ct = default)
        => InvokeAsync("sessions.reset", new { sessionKey }, ct);

    public Task<GatewayResponse> SessionsDeleteAsync(string sessionKey, CancellationToken ct = default)
        => InvokeAsync("sessions.delete", new { sessionKey }, ct);

    public Task<GatewayResponse> SessionsCompactAsync(string sessionKey, CancellationToken ct = default)
        => InvokeAsync("sessions.compact", new { sessionKey }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Heartbeat
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> LastHeartbeatAsync(CancellationToken ct = default)
        => InvokeAsync("last-heartbeat", ct: ct);

    public Task<GatewayResponse> SetHeartbeatsAsync(object config, CancellationToken ct = default)
        => InvokeAsync("set-heartbeats", config, ct);

    // ═══════════════════════════════════════════════════════════
    //  Wake
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> WakeAsync(CancellationToken ct = default)
        => InvokeAsync("wake", ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  Node Pairing
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> NodePairRequestAsync(object request, CancellationToken ct = default)
        => InvokeAsync("node.pair.request", request, ct);

    public Task<GatewayResponse> NodePairListAsync(CancellationToken ct = default)
        => InvokeAsync("node.pair.list", ct: ct);

    public Task<GatewayResponse> NodePairApproveAsync(string requestId, CancellationToken ct = default)
        => InvokeAsync("node.pair.approve", new { requestId }, ct);

    public Task<GatewayResponse> NodePairRejectAsync(string requestId, CancellationToken ct = default)
        => InvokeAsync("node.pair.reject", new { requestId }, ct);

    public Task<GatewayResponse> NodePairVerifyAsync(string nodeId, CancellationToken ct = default)
        => InvokeAsync("node.pair.verify", new { nodeId }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Device Pairing
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> DevicePairListAsync(CancellationToken ct = default)
        => InvokeAsync("device.pair.list", ct: ct);

    public Task<GatewayResponse> DevicePairApproveAsync(string requestId, CancellationToken ct = default)
        => InvokeAsync("device.pair.approve", new { requestId }, ct);

    public Task<GatewayResponse> DevicePairRejectAsync(string requestId, CancellationToken ct = default)
        => InvokeAsync("device.pair.reject", new { requestId }, ct);

    public Task<GatewayResponse> DevicePairRemoveAsync(string deviceId, CancellationToken ct = default)
        => InvokeAsync("device.pair.remove", new { deviceId }, ct);

    public Task<GatewayResponse> DeviceTokenRotateAsync(string deviceId, CancellationToken ct = default)
        => InvokeAsync("device.token.rotate", new { deviceId }, ct);

    public Task<GatewayResponse> DeviceTokenRevokeAsync(string deviceId, CancellationToken ct = default)
        => InvokeAsync("device.token.revoke", new { deviceId }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Node Management
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> NodeRenameAsync(string nodeId, string name, CancellationToken ct = default)
        => InvokeAsync("node.rename", new { nodeId, name }, ct);

    public Task<GatewayResponse> NodeListAsync(CancellationToken ct = default)
        => InvokeAsync("node.list", ct: ct);

    public Task<GatewayResponse> NodeDescribeAsync(string nodeId, CancellationToken ct = default)
        => InvokeAsync("node.describe", new { nodeId }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Node Pending & Invoke
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> NodePendingDrainAsync(string? nodeId = null, CancellationToken ct = default)
        => InvokeAsync("node.pending.drain", new { nodeId }, ct);

    public Task<GatewayResponse> NodePendingEnqueueAsync(object task, CancellationToken ct = default)
        => InvokeAsync("node.pending.enqueue", task, ct);

    public Task<GatewayResponse> NodeInvokeAsync(object invocation, CancellationToken ct = default)
        => InvokeAsync("node.invoke", invocation, ct);

    public Task<GatewayResponse> NodePendingPullAsync(string? nodeId = null, CancellationToken ct = default)
        => InvokeAsync("node.pending.pull", new { nodeId }, ct);

    public Task<GatewayResponse> NodePendingAckAsync(string taskId, CancellationToken ct = default)
        => InvokeAsync("node.pending.ack", new { taskId }, ct);

    public Task<GatewayResponse> NodeInvokeResultAsync(object result, CancellationToken ct = default)
        => InvokeAsync("node.invoke.result", result, ct);

    public Task<GatewayResponse> NodeEventAsync(object eventData, CancellationToken ct = default)
        => InvokeAsync("node.event", eventData, ct);

    public Task<GatewayResponse> NodeCanvasCapabilityRefreshAsync(CancellationToken ct = default)
        => InvokeAsync("node.canvas.capability.refresh", ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  Cron
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> CronListAsync(CancellationToken ct = default)
        => InvokeAsync("cron.list", ct: ct);

    public Task<GatewayResponse> CronStatusAsync(CancellationToken ct = default)
        => InvokeAsync("cron.status", ct: ct);

    public Task<GatewayResponse> CronAddAsync(object cronJob, CancellationToken ct = default)
        => InvokeAsync("cron.add", cronJob, ct);

    public Task<GatewayResponse> CronUpdateAsync(string cronId, object updates, CancellationToken ct = default)
        => InvokeAsync("cron.update", new { cronId, updates }, ct);

    public Task<GatewayResponse> CronRemoveAsync(string cronId, CancellationToken ct = default)
        => InvokeAsync("cron.remove", new { cronId }, ct);

    public Task<GatewayResponse> CronRunAsync(string cronId, CancellationToken ct = default)
        => InvokeAsync("cron.run", new { cronId }, ct);

    public Task<GatewayResponse> CronRunsAsync(string? cronId = null, CancellationToken ct = default)
        => InvokeAsync("cron.runs", new { cronId }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Gateway Identity
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> GatewayIdentityGetAsync(CancellationToken ct = default)
        => InvokeAsync("gateway.identity.get", ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  System
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> SystemPresenceAsync(CancellationToken ct = default)
        => InvokeAsync("system-presence", ct: ct);

    public Task<GatewayResponse> SystemEventAsync(object eventData, CancellationToken ct = default)
        => InvokeAsync("system-event", eventData, ct);

    // ═══════════════════════════════════════════════════════════
    //  Send
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> SendAsync(object message, CancellationToken ct = default)
        => InvokeAsync("send", message, ct);

    // ═══════════════════════════════════════════════════════════
    //  Agent
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> AgentAsync(object? parameters = null, CancellationToken ct = default)
        => InvokeAsync("agent", parameters, ct);

    public Task<GatewayResponse> AgentIdentityGetAsync(CancellationToken ct = default)
        => InvokeAsync("agent.identity.get", ct: ct);

    public Task<GatewayResponse> AgentWaitAsync(CancellationToken ct = default)
        => InvokeAsync("agent.wait", ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  Browser
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> BrowserRequestAsync(object request, CancellationToken ct = default)
        => InvokeAsync("browser.request", request, ct);

    // ═══════════════════════════════════════════════════════════
    //  Chat
    // ═══════════════════════════════════════════════════════════

    public Task<GatewayResponse> ChatHistoryAsync(string? sessionKey = null, CancellationToken ct = default)
        => InvokeAsync("chat.history", new { sessionKey }, ct);

    public Task<GatewayResponse> ChatAbortAsync(string? sessionKey = null, CancellationToken ct = default)
        => InvokeAsync("chat.abort", new { sessionKey }, ct);

    public Task<GatewayResponse> ChatSendAsync(string message, string? sessionKey = null, CancellationToken ct = default)
    {
        var key = sessionKey
                  ?? _helloOk?.Snapshot?.SessionDefaults?.MainSessionKey
                  ?? "agent:main:main";

        return InvokeAsync("chat.send", new ChatSendParams
        {
            SessionKey = key,
            Message = message,
        }, ct);
    }
}
