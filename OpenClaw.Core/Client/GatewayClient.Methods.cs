using System.Text.Json;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// GatewayClient 的 RPC 方法封装（partial）。
/// 每个方法对应网关服务端注册的一个 RPC 方法名，调用后自动在控制台打印关键响应信息。
/// 方法名常量定义于 <see cref="GatewayConstants.Methods"/>。
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

    /// <summary>
    /// 执行健康检查，返回网关的存活状态与各子系统运行摘要。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含健康检查结果</returns>
    public Task<GatewayResponse> HealthAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.Health, ct: ct);

    /// <summary>
    /// 查询 Agent 运行时的内存诊断状态，包括堆使用量、GC 统计等。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含内存诊断数据</returns>
    public Task<GatewayResponse> DoctorMemoryStatusAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.DoctorMemoryStatus, ct: ct);

    /// <summary>
    /// 拉取网关最近的日志行。
    /// </summary>
    /// <param name="lines">要拉取的日志行数，默认 50</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含日志内容数组</returns>
    public Task<GatewayResponse> LogsTailAsync(int lines = 50, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.LogsTail, new { lines }, ct);

    /// <summary>
    /// 查询所有消息渠道（Telegram、Discord、微信等）的连接状态。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含各渠道状态映射</returns>
    public Task<GatewayResponse> ChannelsStatusAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ChannelsStatus, ct: ct);

    /// <summary>
    /// 登出指定的消息渠道，断开其与第三方平台的连接。
    /// </summary>
    /// <param name="channel">要登出的渠道名称（如 "telegram"、"discord"）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> ChannelsLogoutAsync(string channel, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ChannelsLogout, new { channel }, ct);

    /// <summary>
    /// 获取网关的综合运行状态，包括版本、启动时间、已连接客户端数等。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含完整状态快照</returns>
    public Task<GatewayResponse> StatusAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.Status, ct: ct);

    /// <summary>
    /// 查询 LLM 调用的使用量统计信息（Token 数、请求次数等）。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含使用量数据</returns>
    public Task<GatewayResponse> UsageStatusAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.UsageStatus, ct: ct);

    /// <summary>
    /// 查询 LLM 调用的成本估算信息（按模型/按时间段统计费用）。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含成本数据</returns>
    public Task<GatewayResponse> UsageCostAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.UsageCost, ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  TTS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 查询 TTS（文本转语音）子系统的当前状态，包括是否启用、当前 Provider 等。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含 TTS 状态信息</returns>
    public Task<GatewayResponse> TtsStatusAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.TtsStatus, ct: ct);

    /// <summary>
    /// 列出所有可用的 TTS Provider（如 Azure、ElevenLabs、OpenAI 等）及其配置状态。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含 Provider 列表</returns>
    public Task<GatewayResponse> TtsProvidersAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.TtsProviders, ct: ct);

    /// <summary>
    /// 全局启用 TTS 功能。启用后 Agent 回复将自动合成语音。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> TtsEnableAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.TtsEnable, ct: ct);

    /// <summary>
    /// 全局禁用 TTS 功能。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> TtsDisableAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.TtsDisable, ct: ct);

    /// <summary>
    /// 将文本转换为语音音频。
    /// </summary>
    /// <param name="text">要合成语音的文本内容</param>
    /// <param name="voice">可选的语音名称/ID，为 null 时使用默认语音</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含音频数据或音频 URL</returns>
    public Task<GatewayResponse> TtsConvertAsync(string text, string? voice = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.TtsConvert, new { text, voice }, ct);

    /// <summary>
    /// 切换当前使用的 TTS Provider。
    /// </summary>
    /// <param name="provider">目标 Provider 标识符</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> TtsSetProviderAsync(string provider, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.TtsSetProvider, new { provider }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Config
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 获取配置值。传入 key 返回指定键的值，key 为 null 时返回全量配置。
    /// </summary>
    /// <param name="key">配置键（点分路径），为 null 时获取全量配置</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含配置值</returns>
    public Task<GatewayResponse> ConfigGetAsync(string? key = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ConfigGet, new { key }, ct);

    /// <summary>
    /// 设置单个配置项的值。更改不会立即生效，需调用 <see cref="ConfigApplyAsync"/> 应用。
    /// </summary>
    /// <param name="key">配置键（点分路径）</param>
    /// <param name="value">要设置的值</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> ConfigSetAsync(string key, object value, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ConfigSet, new { key, value }, ct);

    /// <summary>
    /// 应用所有待生效的配置变更。变更将立即写入磁盘并热加载到运行时。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> ConfigApplyAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ConfigApply, ct: ct);

    /// <summary>
    /// 批量补丁修改配置。传入的 patch 对象会与现有配置深度合并。
    /// </summary>
    /// <param name="patch">配置补丁对象（部分 JSON 结构）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> ConfigPatchAsync(object patch, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ConfigPatch, new { patch }, ct);

    /// <summary>
    /// 获取完整的配置 JSON Schema，描述所有可配置项的类型、约束和默认值。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含 JSON Schema</returns>
    public Task<GatewayResponse> ConfigSchemaAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ConfigSchema, ct: ct);

    /// <summary>
    /// 查找指定路径对应的 Schema 片段，用于获取特定配置项的类型定义。
    /// </summary>
    /// <param name="path">Schema 路径（如 "llm.model"）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含对应的 Schema 片段</returns>
    public Task<GatewayResponse> ConfigSchemaLookupAsync(string path, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ConfigSchemaLookup, new { path }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Exec Approvals
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 获取当前的工具执行审批策略配置（全局级别）。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含审批策略</returns>
    public Task<GatewayResponse> ExecApprovalsGetAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ExecApprovalsGet, ct: ct);

    /// <summary>
    /// 设置工具执行审批策略（全局级别），定义哪些工具需要人工审批。
    /// </summary>
    /// <param name="approvals">审批策略配置对象</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> ExecApprovalsSetAsync(object approvals, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ExecApprovalsSet, approvals, ct);

    /// <summary>
    /// 获取指定节点的工具执行审批策略。
    /// </summary>
    /// <param name="nodeId">节点 ID，为 null 时返回所有节点的策略</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含节点级审批策略</returns>
    public Task<GatewayResponse> ExecApprovalsNodeGetAsync(string? nodeId = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ExecApprovalsNodeGet, new { nodeId }, ct);

    /// <summary>
    /// 设置指定节点的工具执行审批策略。
    /// </summary>
    /// <param name="nodeId">目标节点 ID</param>
    /// <param name="approvals">节点级审批策略配置对象</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> ExecApprovalsNodeSetAsync(string nodeId, object approvals, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ExecApprovalsNodeSet, new { nodeId, approvals }, ct);

    /// <summary>
    /// 发起一个工具执行审批请求。Agent 执行需要审批的工具时会自动调用此方法。
    /// </summary>
    /// <param name="request">审批请求详情（工具名、命令、参数等）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含 approvalId</returns>
    public Task<GatewayResponse> ExecApprovalRequestAsync(object request, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ExecApprovalRequest, request, ct);

    /// <summary>
    /// 等待指定审批请求的决定结果。此调用会挂起直到审批被批准或拒绝。
    /// </summary>
    /// <param name="approvalId">审批请求 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含审批决定</returns>
    public Task<GatewayResponse> ExecApprovalWaitDecisionAsync(string approvalId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ExecApprovalWaitDecision, new { approvalId }, ct);

    /// <summary>
    /// 解决（批准或拒绝）一个待处理的工具执行审批请求。
    /// </summary>
    /// <param name="approvalId">审批请求 ID</param>
    /// <param name="decision">决定结果（"approve" 或 "reject"）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> ExecApprovalResolveAsync(string approvalId, string decision, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ExecApprovalResolve, new { approvalId, decision }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Wizard
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 启动一个向导流程（如首次配置向导、渠道接入向导等）。
    /// </summary>
    /// <param name="sessionId">关联的会话 ID</param>
    /// <param name="wizardType">向导类型，为 null 时使用默认向导</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含向导初始状态</returns>
    public Task<GatewayResponse> WizardStartAsync(string sessionId, string? wizardType = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.WizardStart, new { sessionId, wizardType }, ct);

    /// <summary>
    /// 推进向导到下一步，传入当前步骤的用户输入。
    /// </summary>
    /// <param name="sessionId">关联的会话 ID</param>
    /// <param name="input">用户在当前步骤的输入数据，为 null 表示跳过</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含下一步的向导状态</returns>
    public Task<GatewayResponse> WizardNextAsync(string sessionId, object? input = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.WizardNext, new { sessionId, input }, ct);

    /// <summary>
    /// 取消正在进行的向导流程，回滚所有未提交的变更。
    /// </summary>
    /// <param name="sessionId">关联的会话 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> WizardCancelAsync(string sessionId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.WizardCancel, new { sessionId }, ct);

    /// <summary>
    /// 获取向导的当前状态（所在步骤、已收集的数据、是否已完成等）。
    /// </summary>
    /// <param name="sessionId">关联的会话 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含向导状态快照</returns>
    public Task<GatewayResponse> WizardStatusAsync(string sessionId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.WizardStatus, new { sessionId }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Talk
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 获取语音对话（Talk）子系统的配置信息，包括 VAD 参数、音频格式等。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含语音配置</returns>
    public Task<GatewayResponse> TalkConfigAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.TalkConfig, ct: ct);

    /// <summary>
    /// 获取或设置语音对话模式（如 push-to-talk、hands-free 等）。
    /// </summary>
    /// <param name="mode">要设置的模式，为 null 时仅查询当前模式</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含当前/更新后的模式</returns>
    public Task<GatewayResponse> TalkModeAsync(string? mode = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.TalkMode, new { mode }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Models & Tools
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 列出网关配置的所有可用 LLM 模型及其元信息（提供商、上下文长度、计费参数等）。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含模型列表</returns>
    public Task<GatewayResponse> ModelsListAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ModelsList, ct: ct);

    /// <summary>
    /// 获取 Agent 已注册的工具完整目录，包括工具名、描述、参数 Schema 等。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含工具目录</returns>
    public Task<GatewayResponse> ToolsCatalogAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ToolsCatalog, ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  Agents
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 列出所有已创建的 Agent 实例及其基本信息。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含 Agent 列表</returns>
    public Task<GatewayResponse> AgentsListAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.AgentsList, ct: ct);

    /// <summary>
    /// 创建一个新的 Agent 实例。
    /// </summary>
    /// <param name="agentConfig">Agent 配置对象（名称、模型、系统提示词等）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含新创建的 Agent 信息</returns>
    public Task<GatewayResponse> AgentsCreateAsync(object agentConfig, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.AgentsCreate, agentConfig, ct);

    /// <summary>
    /// 更新指定 Agent 的配置。
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="updates">要更新的字段（部分更新）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> AgentsUpdateAsync(string agentId, object updates, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.AgentsUpdate, new { agentId, updates }, ct);

    /// <summary>
    /// 删除指定的 Agent 实例及其所有关联会话数据。
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> AgentsDeleteAsync(string agentId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.AgentsDelete, new { agentId }, ct);

    /// <summary>
    /// 列出指定 Agent 的所有附属文件（如知识库文件、自定义指令文件等）。
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含文件列表</returns>
    public Task<GatewayResponse> AgentsFilesListAsync(string agentId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.AgentsFilesList, new { agentId }, ct);

    /// <summary>
    /// 获取指定 Agent 的某个文件内容。
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="fileName">文件名</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含文件内容</returns>
    public Task<GatewayResponse> AgentsFilesGetAsync(string agentId, string fileName, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.AgentsFilesGet, new { agentId, fileName }, ct);

    /// <summary>
    /// 写入或覆盖指定 Agent 的文件内容。
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="fileName">文件名</param>
    /// <param name="content">文件内容</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> AgentsFilesSetAsync(string agentId, string fileName, string content, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.AgentsFilesSet, new { agentId, fileName, content }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Skills
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 查询技能系统的安装与运行状态。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含技能状态概览</returns>
    public Task<GatewayResponse> SkillsStatusAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SkillsStatus, ct: ct);

    /// <summary>
    /// 列出所有可用的技能二进制包及其版本信息。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含二进制包列表</returns>
    public Task<GatewayResponse> SkillsBinsAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SkillsBins, ct: ct);

    /// <summary>
    /// 安装指定的技能包。
    /// </summary>
    /// <param name="skillId">技能 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示安装是否成功</returns>
    public Task<GatewayResponse> SkillsInstallAsync(string skillId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SkillsInstall, new { skillId }, ct);

    /// <summary>
    /// 更新指定的技能包到最新版本。
    /// </summary>
    /// <param name="skillId">技能 ID，为 null 时更新所有已安装技能</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示更新是否成功</returns>
    public Task<GatewayResponse> SkillsUpdateAsync(string? skillId = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SkillsUpdate, new { skillId }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Update
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 触发网关系统更新。网关会下载最新版本并重启。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示更新流程是否成功启动</returns>
    public Task<GatewayResponse> UpdateRunAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.UpdateRun, ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  Voice Wake
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 获取语音唤醒（VoiceWake）的配置，包括唤醒词、灵敏度等参数。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含语音唤醒配置</returns>
    public Task<GatewayResponse> VoicewakeGetAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.VoicewakeGet, ct: ct);

    /// <summary>
    /// 设置语音唤醒配置。
    /// </summary>
    /// <param name="config">唤醒配置对象（唤醒词、灵敏度等）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> VoicewakeSetAsync(object config, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.VoicewakeSet, config, ct);

    // ═══════════════════════════════════════════════════════════
    //  Secrets
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 重新加载密钥存储，从磁盘重新读取所有已配置的密钥。
    /// 当手动修改了密钥文件后需调用此方法。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> SecretsReloadAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SecretsReload, ct: ct);

    /// <summary>
    /// 解析密钥引用，返回指定 key 对应的实际密钥值（脱敏显示）。
    /// </summary>
    /// <param name="key">密钥键名</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含解析后的密钥信息</returns>
    public Task<GatewayResponse> SecretsResolveAsync(string key, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SecretsResolve, new { key }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Sessions
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 列出所有活跃的会话及其摘要信息。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含会话列表</returns>
    public Task<GatewayResponse> SessionsListAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsList, ct: ct);

    /// <summary>
    /// 预览指定会话的内容（最近若干条消息摘要）。
    /// </summary>
    /// <param name="sessionKey">会话键（如 <see cref="GatewayConstants.DefaultSessionKey"/>）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含会话预览</returns>
    public Task<GatewayResponse> SessionsPreviewAsync(string sessionKey, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsPreview, new { sessionKey }, ct);

    /// <summary>
    /// 补丁修改指定会话的属性（如标题、标签等元数据）。
    /// </summary>
    /// <param name="sessionKey">会话键</param>
    /// <param name="patch">补丁对象</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> SessionsPatchAsync(string sessionKey, object patch, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsPatch, new { sessionKey, patch }, ct);

    /// <summary>
    /// 重置指定会话，清空所有对话上下文，恢复到初始状态。
    /// </summary>
    /// <param name="sessionKey">会话键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> SessionsResetAsync(string sessionKey, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsReset, new { sessionKey }, ct);

    /// <summary>
    /// 永久删除指定会话及其所有历史消息数据。
    /// </summary>
    /// <param name="sessionKey">会话键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> SessionsDeleteAsync(string sessionKey, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsDelete, new { sessionKey }, ct);

    /// <summary>
    /// 压缩指定会话的历史上下文。将旧消息合并为摘要以释放 Token 窗口空间。
    /// </summary>
    /// <param name="sessionKey">会话键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> SessionsCompactAsync(string sessionKey, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsCompact, new { sessionKey }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Heartbeat
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 获取最后一次心跳的时间戳和关联的 Agent/会话信息。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含心跳信息</returns>
    public Task<GatewayResponse> LastHeartbeatAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.LastHeartbeat, ct: ct);

    /// <summary>
    /// 设置心跳配置（心跳间隔、超时阈值等）。
    /// </summary>
    /// <param name="config">心跳配置对象</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> SetHeartbeatsAsync(object config, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SetHeartbeats, config, ct);

    // ═══════════════════════════════════════════════════════════
    //  Wake
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 唤醒 Agent，使其从空闲/休眠状态恢复为活跃状态并开始监听输入。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> WakeAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.Wake, ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  Node Pairing
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 发起节点配对请求。节点能力宿主调用此方法向网关注册自身。
    /// </summary>
    /// <param name="request">配对请求详情（节点标识、能力声明等）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含配对请求 ID</returns>
    public Task<GatewayResponse> NodePairRequestAsync(object request, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodePairRequest, request, ct);

    /// <summary>
    /// 列出所有节点配对请求（包括待审批和已完成的）。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含配对请求列表</returns>
    public Task<GatewayResponse> NodePairListAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodePairList, ct: ct);

    /// <summary>
    /// 批准指定的节点配对请求，允许该节点接入网关。
    /// </summary>
    /// <param name="requestId">配对请求 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> NodePairApproveAsync(string requestId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodePairApprove, new { requestId }, ct);

    /// <summary>
    /// 拒绝指定的节点配对请求。
    /// </summary>
    /// <param name="requestId">配对请求 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> NodePairRejectAsync(string requestId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodePairReject, new { requestId }, ct);

    /// <summary>
    /// 验证节点的配对状态是否有效。
    /// </summary>
    /// <param name="nodeId">节点 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含配对验证结果</returns>
    public Task<GatewayResponse> NodePairVerifyAsync(string nodeId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodePairVerify, new { nodeId }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Device Pairing
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 列出所有设备配对请求及已配对设备。返回 pending（待审批）和 approved（已批准）两个列表。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含 <see cref="PairingModels.PairListResponse"/></returns>
    public Task<GatewayResponse> DevicePairListAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.DevicePairList, ct: ct);

    /// <summary>
    /// 批准指定的设备配对请求，允许该设备通过 DeviceToken 免审批重连。
    /// </summary>
    /// <param name="requestId">配对请求 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> DevicePairApproveAsync(string requestId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.DevicePairApprove, new { requestId }, ct);

    /// <summary>
    /// 拒绝指定的设备配对请求。
    /// </summary>
    /// <param name="requestId">配对请求 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> DevicePairRejectAsync(string requestId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.DevicePairReject, new { requestId }, ct);

    /// <summary>
    /// 移除已配对的设备，撤销其 DeviceToken，该设备下次连接需重新配对。
    /// </summary>
    /// <param name="deviceId">设备 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> DevicePairRemoveAsync(string deviceId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.DevicePairRemove, new { deviceId }, ct);

    /// <summary>
    /// 轮换指定设备的 DeviceToken。旧 Token 立即失效，设备需使用新 Token 重连。
    /// </summary>
    /// <param name="deviceId">设备 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含新的 DeviceToken</returns>
    public Task<GatewayResponse> DeviceTokenRotateAsync(string deviceId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.DeviceTokenRotate, new { deviceId }, ct);

    /// <summary>
    /// 吊销指定设备的 DeviceToken，使其无法再通过 Token 免审批连接。
    /// </summary>
    /// <param name="deviceId">设备 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> DeviceTokenRevokeAsync(string deviceId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.DeviceTokenRevoke, new { deviceId }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Node Management
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 重命名指定节点的显示名称。
    /// </summary>
    /// <param name="nodeId">节点 ID</param>
    /// <param name="name">新的显示名称</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> NodeRenameAsync(string nodeId, string name, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodeRename, new { nodeId, name }, ct);

    /// <summary>
    /// 列出所有已配对的节点及其在线状态、能力声明等信息。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含节点列表</returns>
    public Task<GatewayResponse> NodeListAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodeList, ct: ct);

    /// <summary>
    /// 获取指定节点的详细描述，包括能力清单、运行状态、配置参数等。
    /// </summary>
    /// <param name="nodeId">节点 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含节点详情</returns>
    public Task<GatewayResponse> NodeDescribeAsync(string nodeId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodeDescribe, new { nodeId }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Node Pending & Invoke
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 排空指定节点（或所有节点）的待处理任务队列，丢弃所有未消费的任务。
    /// </summary>
    /// <param name="nodeId">节点 ID，为 null 时排空所有节点</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> NodePendingDrainAsync(string? nodeId = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodePendingDrain, new { nodeId }, ct);

    /// <summary>
    /// 向节点的待处理队列中入队一个新任务。
    /// </summary>
    /// <param name="task">任务描述对象</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含入队后的任务 ID</returns>
    public Task<GatewayResponse> NodePendingEnqueueAsync(object task, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodePendingEnqueue, task, ct);

    /// <summary>
    /// 直接调用节点上的某个能力（同步模式），等待节点执行完成后返回结果。
    /// </summary>
    /// <param name="invocation">调用描述对象（目标节点、方法、参数等）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含节点返回的执行结果</returns>
    public Task<GatewayResponse> NodeInvokeAsync(object invocation, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodeInvoke, invocation, ct);

    /// <summary>
    /// 节点侧拉取一个待处理的任务（长轮询模式）。节点宿主在空闲时调用此方法等待新任务。
    /// </summary>
    /// <param name="nodeId">节点 ID，为 null 时拉取当前连接节点的任务</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含待处理任务详情</returns>
    public Task<GatewayResponse> NodePendingPullAsync(string? nodeId = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodePendingPull, new { nodeId }, ct);

    /// <summary>
    /// 节点侧确认已消费（ACK）指定任务，从队列中移除。
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> NodePendingAckAsync(string taskId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodePendingAck, new { taskId }, ct);

    /// <summary>
    /// 节点侧提交能力调用的执行结果。对应 <see cref="NodeInvokeAsync"/> 的异步响应。
    /// </summary>
    /// <param name="result">执行结果对象（invocationId、status、output 等）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> NodeInvokeResultAsync(object result, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodeInvokeResult, result, ct);

    /// <summary>
    /// 节点侧主动推送一个事件到网关（如状态变更通知、进度更新等）。
    /// </summary>
    /// <param name="eventData">事件数据对象</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> NodeEventAsync(object eventData, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodeEvent, eventData, ct);

    /// <summary>
    /// 通知网关刷新节点的 Canvas 能力声明，通常在节点能力变更后调用。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> NodeCanvasCapabilityRefreshAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.NodeCanvasCapabilityRefresh, ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  Cron
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 列出所有已配置的定时任务。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含定时任务列表</returns>
    public Task<GatewayResponse> CronListAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronList, ct: ct);

    /// <summary>
    /// 查询定时任务调度器的运行状态（是否启用、下次执行时间等）。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含调度器状态</returns>
    public Task<GatewayResponse> CronStatusAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronStatus, ct: ct);

    /// <summary>
    /// 添加一个新的定时任务。
    /// </summary>
    /// <param name="cronJob">定时任务配置（cron 表达式、要执行的消息/命令等）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含新创建的任务信息</returns>
    public Task<GatewayResponse> CronAddAsync(object cronJob, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronAdd, cronJob, ct);

    /// <summary>
    /// 更新指定定时任务的配置。
    /// </summary>
    /// <param name="cronId">定时任务 ID</param>
    /// <param name="updates">要更新的字段</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> CronUpdateAsync(string cronId, object updates, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronUpdate, new { cronId, updates }, ct);

    /// <summary>
    /// 移除指定的定时任务。
    /// </summary>
    /// <param name="cronId">定时任务 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> CronRemoveAsync(string cronId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronRemove, new { cronId }, ct);

    /// <summary>
    /// 立即手动触发执行指定的定时任务（不影响其调度计划）。
    /// </summary>
    /// <param name="cronId">定时任务 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示执行是否成功</returns>
    public Task<GatewayResponse> CronRunAsync(string cronId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronRun, new { cronId }, ct);

    /// <summary>
    /// 查询定时任务的历史执行记录。
    /// </summary>
    /// <param name="cronId">定时任务 ID，为 null 时返回所有任务的执行记录</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含执行记录列表</returns>
    public Task<GatewayResponse> CronRunsAsync(string? cronId = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronRuns, new { cronId }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Gateway Identity
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 获取网关自身的身份信息，包括 Gateway ID、名称、版本、所在区域等。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含网关身份信息</returns>
    public Task<GatewayResponse> GatewayIdentityGetAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.GatewayIdentityGet, ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  System
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 获取当前所有已连接设备/客户端的在线状态快照。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含在线设备列表</returns>
    public Task<GatewayResponse> SystemPresenceAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SystemPresence, ct: ct);

    /// <summary>
    /// 向网关推送一个系统级事件（如外部告警、第三方回调通知等）。
    /// </summary>
    /// <param name="eventData">事件数据对象</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> SystemEventAsync(object eventData, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SystemEvent, eventData, ct);

    // ═══════════════════════════════════════════════════════════
    //  Send
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 通用消息发送接口，向指定目标投递任意格式的消息。
    /// </summary>
    /// <param name="message">消息对象（包含目标、内容类型、数据等）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> SendAsync(object message, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.Send, message, ct);

    // ═══════════════════════════════════════════════════════════
    //  Agent
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Agent 通用操作接口，用于发送自定义控制指令到 Agent 运行时。
    /// </summary>
    /// <param name="parameters">操作参数，为 null 时发送空请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应</returns>
    public Task<GatewayResponse> AgentAsync(object? parameters = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.Agent, parameters, ct);

    /// <summary>
    /// 获取当前活跃 Agent 的身份信息（名称、模型、系统提示词摘要等）。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含 Agent 身份信息</returns>
    public Task<GatewayResponse> AgentIdentityGetAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.AgentIdentityGet, ct: ct);

    /// <summary>
    /// 等待 Agent 完成初始化并进入就绪状态。适用于 Agent 启动较慢的场景。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，Agent 就绪后返回</returns>
    public Task<GatewayResponse> AgentWaitAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.AgentWait, ct: ct);

    // ═══════════════════════════════════════════════════════════
    //  Browser
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 发起一个浏览器操作请求（如打开 URL、截图、点击元素等），由 Agent 的浏览器工具执行。
    /// </summary>
    /// <param name="request">浏览器操作请求对象</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含浏览器操作结果</returns>
    public Task<GatewayResponse> BrowserRequestAsync(object request, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.BrowserRequest, request, ct);

    // ═══════════════════════════════════════════════════════════
    //  Chat
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 获取指定会话的聊天历史消息记录。
    /// </summary>
    /// <param name="sessionKey">会话键，为 null 时使用默认会话</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含消息历史数组</returns>
    public Task<GatewayResponse> ChatHistoryAsync(string? sessionKey = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ChatHistory, new { sessionKey }, ct);

    /// <summary>
    /// 中止当前正在进行的聊天回合。Agent 将立即停止生成并返回已生成的部分。
    /// </summary>
    /// <param name="sessionKey">会话键，为 null 时中止默认会话</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> ChatAbortAsync(string? sessionKey = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ChatAbort, new { sessionKey }, ct);

    /// <summary>
    /// 向指定会话发送一条用户消息，触发 Agent 生成回复。
    /// 回复内容通过 <see cref="GatewayConstants.Events.Agent"/> 事件流式推送。
    /// 若未指定 sessionKey，依次使用：服务端下发的主会话键 → <see cref="GatewayConstants.DefaultSessionKey"/>。
    /// </summary>
    /// <param name="message">用户消息文本</param>
    /// <param name="sessionKey">目标会话键，为 null 时自动选择</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示消息是否成功入队处理</returns>
    public Task<GatewayResponse> ChatSendAsync(string message, string? sessionKey = null, CancellationToken ct = default)
    {
        var key = sessionKey
                  ?? _helloOk?.Snapshot?.SessionDefaults?.MainSessionKey
                  ?? GatewayConstants.DefaultSessionKey;

        return InvokeAsync(GatewayConstants.Methods.ChatSend, new ChatSendParams
        {
            SessionKey = key,
            Message = message,
        }, ct);
    }
}
