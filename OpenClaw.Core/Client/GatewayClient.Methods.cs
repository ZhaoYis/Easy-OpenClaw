using System.Text.Json;
using OpenClaw.Core.Helpers;
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
            if (IsTransientError(err))
                Log.Warn($"[{method}] 服务暂时不可用 → {err}");
            else
                Log.Error($"[{method}] 失败 → {err}");
        }

        return resp;
    }

    /// <summary>
    /// 当服务端未在 hello-ok 中声明某 RPC 时，构造本地「跳过」响应，避免向网关发送必然失败的请求。
    /// </summary>
    private static GatewayResponse SkippedRpcResponseNotAdvertised(string method) =>
        new()
        {
            Ok = true,
            Id = "",
            Payload = JsonSerializer.SerializeToElement(
                new { skipped = true, reason = "not_advertised", method },
                JsonDefaults.SerializerOptions),
        };

    /// <summary>
    /// 带重试的调用入口。对瞬态错误（UNAVAILABLE、DEADLINE_EXCEEDED 等）自动重试。
    /// </summary>
    /// <param name="method">RPC 方法名</param>
    /// <param name="parameters">请求参数，为 null 时发送空对象</param>
    /// <param name="ct">取消令牌</param>
    /// <param name="maxRetries">最大重试次数，默认 1</param>
    internal async Task<GatewayResponse> InvokeWithRetryAsync(
        string method, object? parameters = null, CancellationToken ct = default, int maxRetries = 1)
    {
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var resp = await InvokeAsync(method, parameters, ct);
            if (resp.Ok || attempt == maxRetries)
                return resp;

            var err = resp.Error?.GetRawText() ?? "";
            if (!IsTransientError(err))
                return resp;

            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            Log.Info($"[{method}] 将在 {delay.TotalSeconds:F0}s 后重试 ({attempt + 1}/{maxRetries})…");
            await Task.Delay(delay, ct);
        }

        return await InvokeAsync(method, parameters, ct);
    }

    private static bool IsTransientError(string errorText) =>
        errorText.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase)
        || errorText.Contains("DEADLINE_EXCEEDED", StringComparison.OrdinalIgnoreCase)
        || errorText.Contains("AbortError", StringComparison.OrdinalIgnoreCase);

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
    /// 拉取网关文件日志的尾部内容，支持游标分页和最大字节数限制。
    /// </summary>
    /// <param name="tailParams">日志拉取参数，包含 limit（行数）、cursor（分页游标）、maxBytes（字节上限）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含日志内容数组及下一页 cursor</returns>
    public Task<GatewayResponse> LogsTailAsync(LogsTailParams tailParams, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.LogsTail, tailParams, ct);

    /// <summary>
    /// 拉取网关文件日志的尾部内容（便捷重载）。
    /// </summary>
    /// <param name="limit">返回的最大日志行数，默认 50</param>
    /// <param name="cursor">分页游标，首次调用传 null</param>
    /// <param name="maxBytes">单次响应最大字节数限制，为 null 时使用网关默认值</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含日志内容数组及下一页 cursor</returns>
    public Task<GatewayResponse> LogsTailAsync(int limit = 50, string? cursor = null, int? maxBytes = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.LogsTail, new LogsTailParams
        {
            Limit = limit,
            Cursor = cursor,
            MaxBytes = maxBytes,
        }, ct);

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

    // ═══════════════════════════════════════════════════════════
    //  Web Login
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 启动 QR/Web 登录流程。针对支持二维码扫码登录的 Web 渠道提供方（如微信等），
    /// 返回登录 URL 或二维码数据，供客户端渲染二维码并等待用户扫码确认。
    /// </summary>
    /// <param name="channel">目标渠道名称（如 "wechat"），必须是支持 QR 登录的 Web 渠道</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含登录 URL 或二维码数据</returns>
    public Task<GatewayResponse> WebLoginStartAsync(string channel, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.WebLoginStart, new { channel }, ct);

    /// <summary>
    /// 等待 QR/Web 登录流程完成。此调用会挂起直到用户扫码确认或超时。
    /// 登录成功后网关会自动启动对应的渠道连接。
    /// </summary>
    /// <param name="channel">目标渠道名称，须与 <see cref="WebLoginStartAsync"/> 中指定的渠道一致</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含登录结果（成功时含渠道启动确认）</returns>
    public Task<GatewayResponse> WebLoginWaitAsync(string channel, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.WebLoginWait, new { channel }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Push
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 向已注册的 iOS 节点发送一条测试 APNs 推送通知，用于验证推送通道是否正常工作。
    /// </summary>
    /// <param name="nodeId">目标 iOS 节点 ID，该节点必须已完成 APNs 注册</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示测试推送是否成功发送</returns>
    public Task<GatewayResponse> PushTestAsync(string nodeId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.PushTest, new { nodeId }, ct);

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
    /// 查询 LLM 调用的聚合成本统计信息（按模型/按时间段统计费用）。
    /// 支持可选的日期范围过滤，未指定时由网关返回默认周期的汇总。
    /// </summary>
    /// <param name="from">统计起始日期（ISO 8601 格式，如 "2026-01-01"），为 null 时由网关决定默认起始</param>
    /// <param name="to">统计结束日期（ISO 8601 格式，如 "2026-04-05"），为 null 时由网关决定默认结束</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含聚合成本数据</returns>
    public Task<GatewayResponse> UsageCostAsync(string? from = null, string? to = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.UsageCost, new { from, to }, ct);

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
    /// 验证并替换完整配置载荷。传入的 config 对象将经过 Schema 验证后
    /// 整体替换当前运行时配置，适用于批量导入或一次性替换整个配置的场景。
    /// </summary>
    /// <param name="config">完整的配置载荷对象（必须通过 Schema 验证）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> ConfigApplyAsync(object config, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ConfigApply, new { config }, ct);

    /// <summary>
    /// 应用所有通过 <see cref="ConfigSetAsync"/> 和 <see cref="ConfigPatchAsync"/> 暂存的配置变更。
    /// 暂存的变更将经过验证后写入磁盘并热加载到运行时。
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
    /// 查询单个执行审批请求的详情（含当前状态、请求方信息、审批决定等）。
    /// </summary>
    /// <param name="approvalId">审批请求 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含审批请求详情</returns>
    public Task<GatewayResponse> ExecApprovalGetAsync(string approvalId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ExecApprovalGet, new { approvalId }, ct);

    /// <summary>
    /// 列出所有待处理和已完成的执行审批请求（含 pending 审批查询/replay）。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含审批请求列表</returns>
    public Task<GatewayResponse> ExecApprovalListAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ExecApprovalList, ct: ct);

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
    //  Plugin Approvals
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 发起一个插件自定义审批请求。插件在执行敏感操作前调用此方法，
    /// 向操作员发送审批请求并获取 approvalId，用于后续查询或等待审批结果。
    /// </summary>
    /// <param name="request">审批请求详情（插件名、操作描述、上下文数据等）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含 approvalId</returns>
    public Task<GatewayResponse> PluginApprovalRequestAsync(object request, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.PluginApprovalRequest, request, ct);

    /// <summary>
    /// 列出所有待处理和已完成的插件审批请求。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含插件审批请求列表</returns>
    public Task<GatewayResponse> PluginApprovalListAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.PluginApprovalList, ct: ct);

    /// <summary>
    /// 等待指定插件审批请求的决定结果。此调用会挂起直到审批被批准/拒绝或超时。
    /// 超时时返回的 payload 中 decision 为 null，表示无人响应。
    /// </summary>
    /// <param name="approvalId">审批请求 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含审批决定（或超时时为 null）</returns>
    public Task<GatewayResponse> PluginApprovalWaitDecisionAsync(string approvalId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.PluginApprovalWaitDecision, new { approvalId }, ct);

    /// <summary>
    /// 解决（批准或拒绝）一个待处理的插件审批请求。由操作员调用。
    /// </summary>
    /// <param name="approvalId">审批请求 ID</param>
    /// <param name="decision">决定结果（"approve" 或 "reject"）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> PluginApprovalResolveAsync(string approvalId, string decision, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.PluginApprovalResolve, new { approvalId, decision }, ct);

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
    /// 获取语音对话（Talk）子系统的有效配置信息，包括 VAD 参数、音频格式等。
    /// 当 <paramref name="includeSecrets"/> 为 true 时，返回的配置中包含敏感凭据（如 API Key），
    /// 此操作需要 operator.talk.secrets 或 operator.admin 权限。
    /// </summary>
    /// <param name="includeSecrets">是否在返回的配置中包含敏感密钥信息，默认 false</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含语音配置</returns>
    public Task<GatewayResponse> TalkConfigAsync(bool includeSecrets = false, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.TalkConfig, new { includeSecrets }, ct);

    /// <summary>
    /// 获取或设置语音对话模式（如 push-to-talk、hands-free 等）。
    /// 设置模式后会自动广播状态变更给所有已连接的 WebChat/Control UI 客户端。
    /// </summary>
    /// <param name="mode">要设置的模式，为 null 时仅查询当前模式</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含当前/更新后的模式</returns>
    public Task<GatewayResponse> TalkModeAsync(string? mode = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.TalkMode, new { mode }, ct);

    /// <summary>
    /// 通过当前活跃的 Talk 语音提供商合成语音并播放。
    /// 与 <see cref="TtsConvertAsync"/> 不同，此方法面向实时语音对话场景，
    /// 使用 Talk 子系统配置的 speech provider 而非 TTS provider。
    /// </summary>
    /// <param name="text">要合成并播放的文本内容</param>
    /// <param name="voice">可选的语音名称/ID，为 null 时使用 Talk 配置中的默认语音</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含合成结果或音频数据</returns>
    public Task<GatewayResponse> TalkSpeakAsync(string text, string? voice = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.TalkSpeak, new { text, voice }, ct);

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
    /// 获取 Agent 已注册的工具完整目录，包括工具名、描述、参数 Schema、来源（core/plugin）等。
    /// 等价于无过滤条件的 <see cref="ToolsCatalogAsync(ToolsCatalogParams, CancellationToken)"/>。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含工具目录</returns>
    public Task<GatewayResponse> ToolsCatalogAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ToolsCatalog, new ToolsCatalogParams(), ct);

    /// <summary>
    /// 按可选 Agent 与插件包含策略获取工具目录（与网关 <c>ToolsCatalogParamsSchema</c> 一致）。
    /// </summary>
    /// <param name="parameters">目录查询参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含工具目录</returns>
    public Task<GatewayResponse> ToolsCatalogAsync(ToolsCatalogParams parameters, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ToolsCatalog, parameters, ct);

    /// <summary>
    /// 获取指定会话当前「运行时生效」的工具清单（含 channel 工具），需 operator.read。
    /// 会话上下文由网关在服务端根据 <paramref name="sessionKey"/> 推导，调用方不可伪造投递上下文。
    /// </summary>
    /// <param name="sessionKey">会话键（必填）</param>
    /// <param name="agentId">可选 Agent ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 含 profile 与按来源分组的工具列表</returns>
    public Task<GatewayResponse> ToolsEffectiveAsync(string sessionKey, string? agentId = null, CancellationToken ct = default)
        => ToolsEffectiveAsync(new ToolsEffectiveParams { SessionKey = sessionKey, AgentId = agentId }, ct);

    /// <summary>
    /// 获取指定会话当前「运行时生效」的工具清单（强类型参数版本）。
    /// </summary>
    /// <param name="parameters">含必填 <see cref="ToolsEffectiveParams.SessionKey"/> 的请求参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 含有效工具分组</returns>
    public Task<GatewayResponse> ToolsEffectiveAsync(ToolsEffectiveParams parameters, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ToolsEffective, parameters, ct);

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
    /// 查询技能系统的安装与运行状态（可选指定 Agent，省略时使用默认 Agent 工作区）。
    /// </summary>
    /// <param name="agentId">Agent ID，为 null 时不传该字段</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含技能状态概览</returns>
    public Task<GatewayResponse> SkillsStatusAsync(string? agentId = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SkillsStatus, new { agentId }, ct);

    /// <summary>
    /// 列出所有可用的技能二进制包及其版本信息。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含二进制包列表</returns>
    public Task<GatewayResponse> SkillsBinsAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SkillsBins, ct: ct);

    /// <summary>
    /// 在 ClawHub 上按关键词搜索技能发现信息（operator.read），使用默认分页（无 query 时由网关决定结果集）。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 含评分与 slug 等元数据列表</returns>
    public Task<GatewayResponse> SkillsSearchAsync(CancellationToken ct = default)
        => SkillsSearchAsync(new SkillsSearchParams(), ct);

    /// <summary>
    /// 在 ClawHub 上按关键词搜索技能发现信息（operator.read）。
    /// </summary>
    /// <param name="parameters">查询与分页参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 含评分与 slug 等元数据列表</returns>
    public Task<GatewayResponse> SkillsSearchAsync(SkillsSearchParams parameters, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SkillsSearch, parameters, ct);

    /// <summary>
    /// 获取 ClawHub 上指定 slug 的技能详情（operator.read）。
    /// </summary>
    /// <param name="slug">技能 slug（非空）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 含技能元数据与版本信息</returns>
    public Task<GatewayResponse> SkillsDetailAsync(string slug, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SkillsDetail, new { slug }, ct);

    /// <summary>
    /// 安装技能。传入 ClawHub 或网关安装器形态的完整参数对象（与 <c>SkillsInstallParamsSchema</c> 联合类型一致）。
    /// </summary>
    /// <param name="installParams">安装请求体（如 <c>{ source:"clawhub", slug }</c> 或 <c>{ name, installId }</c>）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示安装是否成功</returns>
    public Task<GatewayResponse> SkillsInstallAsync(object installParams, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SkillsInstall, installParams, ct);

    /// <summary>
    /// 从 ClawHub 按 slug 安装技能（便捷封装：<c>source=clawhub</c>）。
    /// </summary>
    /// <param name="slug">ClawHub 技能 slug</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示安装是否成功</returns>
    public Task<GatewayResponse> SkillsInstallFromClawHubAsync(string slug, CancellationToken ct = default)
        => SkillsInstallAsync(new { source = "clawhub", slug }, ct);

    /// <summary>
    /// 更新技能：传入配置补丁或 ClawHub 更新形态的完整对象（与 <c>SkillsUpdateParamsSchema</c> 联合类型一致）。
    /// </summary>
    /// <param name="updateParams">更新请求体</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示更新是否成功</returns>
    public Task<GatewayResponse> SkillsUpdateAsync(object updateParams, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SkillsUpdate, updateParams, ct);

    /// <summary>
    /// 将 ClawHub 已跟踪技能更新到最新版本（便捷封装：<c>source=clawhub</c>）。
    /// </summary>
    /// <param name="slug">要更新的单个技能 slug；与 <paramref name="updateAll"/> 二选一或优先使用 <paramref name="updateAll"/></param>
    /// <param name="updateAll">为 true 时更新工作区内所有已跟踪的 ClawHub 技能</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示更新是否成功</returns>
    public Task<GatewayResponse> SkillsUpdateClawHubAsync(string? slug = null, bool updateAll = false, CancellationToken ct = default)
    {
        if (updateAll)
            return SkillsUpdateAsync(new { source = "clawhub", all = true }, ct);
        return SkillsUpdateAsync(new { source = "clawhub", slug }, ct);
    }

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
    /// 解析指定命令/目标集合的密钥分配（command-target secret assignments）。
    /// 根据 command 和 target 标识，解析运行时 SecretRef 绑定，
    /// 返回匹配的密钥分配结果。
    /// </summary>
    /// <param name="command">命令标识（如工具名、插件命令等）</param>
    /// <param name="target">目标标识（如渠道名、服务端点等）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含解析后的密钥分配信息</returns>
    public Task<GatewayResponse> SecretsResolveAsync(string command, string target, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SecretsResolve, new { command, target }, ct);

    // ═══════════════════════════════════════════════════════════
    //  Sessions
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 列出所有活跃的会话及其摘要信息，返回当前会话索引。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含会话列表与当前索引</returns>
    public Task<GatewayResponse> SessionsListAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsList, ct: ct);

    /// <summary>
    /// 为当前连接打开会话变更推送（官方：toggle session change event subscriptions for the current WS client）。
    /// </summary>
    /// <remarks>
    /// 发送的是 <c>type:&quot;req&quot;</c>、<c>method:&quot;sessions.subscribe&quot;</c>；成功后本连接会收到 <c>type:&quot;event&quot;</c> 的
    /// <see cref="GatewayConstants.Events.SessionsChanged"/>（<c>sessions.changed</c>）。名称上带 subscribe，但不是事件帧本身。
    /// 若 hello-ok 的 <c>features.methods</c> 非空且未列出该方法，则跳过请求并返回本地 <c>skipped</c> 载荷，避免旧网关 <c>unknown method</c>。
    /// </remarks>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示订阅是否成功；未声明时返回带 <c>skipped</c> 的本地响应</returns>
    public Task<GatewayResponse> SessionsSubscribeAsync(CancellationToken ct = default)
    {
        const string method = GatewayConstants.Methods.SessionsSubscribe;
        if (IsRpcMethodAdvertised(method) == false)
        {
            Log.Info(
                $"[{method}] 未在 hello-ok.features.methods 中声明，跳过调用（请升级网关以接收 {GatewayConstants.Events.SessionsChanged}）");
            return Task.FromResult(SkippedRpcResponseNotAdvertised(method));
        }

        return InvokeAsync(method, ct: ct);
    }

    /// <summary>
    /// 为当前连接关闭会话变更推送（与 <see cref="SessionsSubscribeAsync"/> 配对）。
    /// </summary>
    /// <remarks>仍为 <c>req.method</c>；在未声明 <c>sessions.unsubscribe</c> 时与订阅方法相同策略跳过。</remarks>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示取消订阅是否成功</returns>
    public Task<GatewayResponse> SessionsUnsubscribeAsync(CancellationToken ct = default)
    {
        const string method = GatewayConstants.Methods.SessionsUnsubscribe;
        if (IsRpcMethodAdvertised(method) == false)
        {
            Log.Info($"[{method}] 未在 hello-ok.features.methods 中声明，跳过调用");
            return Task.FromResult(SkippedRpcResponseNotAdvertised(method));
        }

        return InvokeAsync(method, ct: ct);
    }

    /// <summary>
    /// 为当前连接打开某一会话的 transcript/message 推送（官方：toggle transcript/message event subscriptions for one session）。
    /// </summary>
    /// <remarks>
    /// 使用 <c>req.method = sessions.messages.subscribe</c>，参数 JSON 字段为 <c>key</c>。
    /// 若 hello-ok 的 <c>features.methods</c> 非空且未列出该方法，则跳过并返回本地 <c>skipped</c> 载荷。
    /// </remarks>
    /// <param name="keyParams">含规范会话 <c>key</c> 的参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应；成功后本连接可收到 <see cref="GatewayConstants.Events.SessionMessage"/> / <see cref="GatewayConstants.Events.SessionTool"/></returns>
    public Task<GatewayResponse> SessionsMessagesSubscribeAsync(SessionsMessagesKeyParams keyParams, CancellationToken ct = default)
    {
        const string method = GatewayConstants.Methods.SessionsMessagesSubscribe;
        if (IsRpcMethodAdvertised(method) == false)
        {
            Log.Info($"[{method}] 未在 hello-ok.features.methods 中声明，跳过调用");
            return Task.FromResult(SkippedRpcResponseNotAdvertised(method));
        }

        return InvokeAsync(method, keyParams, ct);
    }

    /// <summary>
    /// 订阅指定会话的 transcript/message 事件（便捷重载，等价于传入 <see cref="SessionsMessagesKeyParams"/>）。
    /// </summary>
    /// <param name="sessionKey">会话键，序列化为协议字段 <c>key</c></param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示订阅是否成功</returns>
    public Task<GatewayResponse> SessionsMessagesSubscribeAsync(string sessionKey, CancellationToken ct = default)
        => SessionsMessagesSubscribeAsync(new SessionsMessagesKeyParams { Key = sessionKey }, ct);

    /// <summary>
    /// 关闭某一会话的 transcript/message 推送（与 <see cref="SessionsMessagesSubscribeAsync(SessionsMessagesKeyParams, CancellationToken)"/> 配对）。
    /// </summary>
    /// <remarks>在未声明 <c>sessions.messages.unsubscribe</c> 时与订阅相同策略跳过。</remarks>
    /// <param name="keyParams">含会话 <c>key</c> 的参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应</returns>
    public Task<GatewayResponse> SessionsMessagesUnsubscribeAsync(SessionsMessagesKeyParams keyParams, CancellationToken ct = default)
    {
        const string method = GatewayConstants.Methods.SessionsMessagesUnsubscribe;
        if (IsRpcMethodAdvertised(method) == false)
        {
            Log.Info($"[{method}] 未在 hello-ok.features.methods 中声明，跳过调用");
            return Task.FromResult(SkippedRpcResponseNotAdvertised(method));
        }

        return InvokeAsync(method, keyParams, ct);
    }

    /// <summary>
    /// 取消订阅指定会话的 transcript/message 事件（便捷重载）。
    /// </summary>
    /// <param name="sessionKey">会话键，序列化为 <c>key</c></param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应</returns>
    public Task<GatewayResponse> SessionsMessagesUnsubscribeAsync(string sessionKey, CancellationToken ct = default)
        => SessionsMessagesUnsubscribeAsync(new SessionsMessagesKeyParams { Key = sessionKey }, ct);

    /// <summary>
    /// 返回指定会话键列表的有界 transcript 预览。
    /// 每个会话仅返回最近若干条消息摘要，适用于会话列表 UI 展示。
    /// </summary>
    /// <param name="sessionKeys">要预览的会话键数组</param>
    /// <param name="limit">每个会话返回的最大消息条数，为 null 时由网关决定默认值</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含各会话的预览数据</returns>
    public Task<GatewayResponse> SessionsPreviewAsync(string[] sessionKeys, int? limit = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsPreview, new SessionsPreviewParams
        {
            Keys = sessionKeys,
            Limit = limit,
        }, ct);

    /// <summary>
    /// 预览指定会话的内容（最近若干条消息摘要）。单会话便捷重载；RPC 仍发送必填数组字段 <c>keys</c>。
    /// </summary>
    /// <param name="sessionKey">会话键（如 <see cref="GatewayConstants.DefaultSessionKey"/>）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含会话预览</returns>
    public Task<GatewayResponse> SessionsPreviewAsync(string sessionKey, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsPreview, new SessionsPreviewParams
        {
            Keys = [sessionKey],
        }, ct);

    /// <summary>
    /// 解析/规范化会话目标。请求体字段与上游网关 schema 一致（如 <c>key</c>、<c>sessionId</c>、<c>label</c> 等）。
    /// </summary>
    /// <param name="resolveParams">完整解析参数；须满足「key / sessionId / label 恰好其一」等网关规则。</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含解析结果</returns>
    public Task<GatewayResponse> SessionsResolveAsync(SessionsResolveParams resolveParams, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsResolve, resolveParams, ct);

    /// <summary>
    /// 按会话键解析的便捷重载。序列化为 RPC 参数 <c>key</c>（非 <c>sessionKey</c> / <c>target</c>）。
    /// </summary>
    /// <param name="key">待解析的会话键（可为部分键等）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含解析后的规范信息</returns>
    public Task<GatewayResponse> SessionsResolveAsync(string key, CancellationToken ct = default)
        => SessionsResolveAsync(new SessionsResolveParams { Key = key }, ct);

    /// <summary>
    /// 创建一个新的会话条目。可指定 Agent、键名、标题、标签和配置覆盖。
    /// </summary>
    /// <param name="createParams">会话创建参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含新创建的会话信息</returns>
    public Task<GatewayResponse> SessionsCreateAsync(SessionsCreateParams createParams, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsCreate, createParams, ct);

    /// <summary>
    /// 创建一个新的会话条目（便捷重载）。使用默认 Agent 并自动生成会话键。
    /// </summary>
    /// <param name="title">会话标题，为 null 时由网关自动生成</param>
    /// <param name="agentId">Agent 标识符，为 null 时使用默认 Agent</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含新创建的会话信息</returns>
    public Task<GatewayResponse> SessionsCreateAsync(string? title = null, string? agentId = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsCreate, new SessionsCreateParams
        {
            Title = title,
            AgentId = agentId,
        }, ct);

    /// <summary>
    /// 向已有会话发送一条消息，触发 Agent 在该会话中生成回复。
    /// 与 <see cref="ChatSendAsync"/> 不同，此方法通过 sessions 域而非 chat 域路由，
    /// 适用于会话管理面板中直接向任意会话发送消息的场景。
    /// </summary>
    /// <param name="sendParams">会话发送参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示消息是否成功入队处理</returns>
    public Task<GatewayResponse> SessionsSendAsync(SessionsSendParams sendParams, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsSend, sendParams, ct);

    /// <summary>
    /// 向已有会话发送一条消息（便捷重载）。
    /// </summary>
    /// <param name="sessionKey">目标会话键</param>
    /// <param name="message">消息文本</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示消息是否成功入队处理</returns>
    public Task<GatewayResponse> SessionsSendAsync(string sessionKey, string message, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsSend, new SessionsSendParams
        {
            SessionKey = sessionKey,
            Message = message,
        }, ct);

    /// <summary>
    /// 中断当前活跃会话并以新指令转向（interrupt-and-steer）。
    /// 如果会话中 Agent 正在生成回复，将先中止当前生成，然后以新消息重新触发回复。
    /// 适用于用户改变意图、紧急纠偏等场景。
    /// </summary>
    /// <param name="steerParams">转向参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示转向操作是否成功</returns>
    public Task<GatewayResponse> SessionsSteerAsync(SessionsSteerParams steerParams, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsSteer, steerParams, ct);

    /// <summary>
    /// 中断当前活跃会话并以新指令转向（便捷重载）。
    /// </summary>
    /// <param name="sessionKey">目标会话键</param>
    /// <param name="message">新的转向消息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示转向操作是否成功</returns>
    public Task<GatewayResponse> SessionsSteerAsync(string sessionKey, string message, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsSteer, new SessionsSteerParams
        {
            SessionKey = sessionKey,
            Message = message,
        }, ct);

    /// <summary>
    /// 中止指定会话当前正在进行的工作。Agent 将立即停止生成并返回已生成的部分。
    /// 与 <see cref="ChatAbortAsync"/> 不同，此方法通过 sessions 域路由，
    /// 适用于会话管理面板中精确控制特定会话的场景。
    /// </summary>
    /// <param name="sessionKey">会话键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示中止操作是否成功</returns>
    public Task<GatewayResponse> SessionsAbortAsync(string sessionKey, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsAbort, new { sessionKey }, ct);

    /// <summary>
    /// 补丁修改指定会话的属性（如标题、标签等元数据）或配置覆盖。
    /// </summary>
    /// <param name="sessionKey">会话键</param>
    /// <param name="patch">补丁对象（部分更新的字段）</param>
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

    /// <summary>
    /// 获取指定会话的完整存储行数据，包括所有消息、元数据和配置覆盖。
    /// </summary>
    /// <param name="sessionKey">会话键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含完整的会话行数据</returns>
    public Task<GatewayResponse> SessionsGetAsync(string sessionKey, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsGet, new { sessionKey }, ct);

    /// <summary>
    /// 查询使用量摘要（单会话或多会话），参数与上游 <c>SessionsUsageParamsSchema</c> 一致。
    /// </summary>
    /// <param name="usageParams">含 <c>key</c>、日期范围等；<c>key</c> 为 null 时可查询多会话汇总（视网关行为而定）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含使用量摘要</returns>
    public Task<GatewayResponse> SessionsUsageAsync(SessionsUsageParams usageParams, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsUsage, usageParams, ct);

    /// <summary>
    /// 查询指定会话的使用量摘要（便捷重载）；序列化为协议字段 <c>key</c>（非 <c>sessionKey</c>）。
    /// </summary>
    /// <param name="sessionKey">会话键（如 <see cref="GatewayConstants.DefaultSessionKey"/>）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含会话级使用量摘要</returns>
    public Task<GatewayResponse> SessionsUsageAsync(string sessionKey, CancellationToken ct = default)
        => SessionsUsageAsync(new SessionsUsageParams { Key = sessionKey }, ct);

    /// <summary>
    /// 查询指定会话的使用量时序数据（上游仅接受必填 <c>key</c>；聚合范围由网关内部策略决定）。
    /// </summary>
    /// <param name="timeseriesParams">至少包含会话 <c>key</c></param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含时序数据</returns>
    public Task<GatewayResponse> SessionsUsageTimeseriesAsync(
        SessionsUsageTimeseriesParams timeseriesParams,
        CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsUsageTimeseries, timeseriesParams, ct);

    /// <summary>
    /// 查询指定会话的使用量时序数据（便捷重载）。
    /// </summary>
    /// <param name="sessionKey">会话键，序列化为 <c>key</c></param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应</returns>
    public Task<GatewayResponse> SessionsUsageTimeseriesAsync(string sessionKey, CancellationToken ct = default)
        => SessionsUsageTimeseriesAsync(new SessionsUsageTimeseriesParams { Key = sessionKey }, ct);

    /// <summary>
    /// 查询指定会话的使用量日志条目（LLM 调用明细）；参数为 <c>key</c> 与可选 <c>limit</c>。
    /// </summary>
    /// <param name="logsParams">会话 <c>key</c> 与可选 <c>limit</c></param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 含 <c>logs</c> 数组</returns>
    public Task<GatewayResponse> SessionsUsageLogsAsync(SessionsUsageLogsParams logsParams, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.SessionsUsageLogs, logsParams, ct);

    /// <summary>
    /// 查询指定会话的使用量日志条目（便捷重载）。
    /// </summary>
    /// <param name="sessionKey">会话键，序列化为 <c>key</c></param>
    /// <param name="limit">最大条数，为 null 时使用网关默认（如 200）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应</returns>
    public Task<GatewayResponse> SessionsUsageLogsAsync(string sessionKey, int? limit = null, CancellationToken ct = default)
        => SessionsUsageLogsAsync(new SessionsUsageLogsParams { Key = sessionKey, Limit = limit }, ct);

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
    /// 调度一次「唤醒」文本注入：由 <see cref="WakeParams.Mode"/> 决定立即下发（<see cref="WakeScheduleMode.Now"/>）
    /// 还是挂起到下一次心跳（<see cref="WakeScheduleMode.NextHeartbeat"/>）。与网关 <c>WakeParamsSchema</c> 一致。
    /// </summary>
    /// <param name="parameters">含 mode 与 text 的请求参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示调度是否被接受</returns>
    public Task<GatewayResponse> WakeAsync(WakeParams parameters, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.Wake, parameters, ct);

    /// <summary>
    /// 使用给定模式调度唤醒文本注入的便捷重载。
    /// </summary>
    /// <param name="text">注入文本（非空）</param>
    /// <param name="mode"><see cref="WakeScheduleMode.Now"/> 或 <see cref="WakeScheduleMode.NextHeartbeat"/></param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示调度是否被接受</returns>
    public Task<GatewayResponse> WakeAsync(string text, string mode, CancellationToken ct = default)
        => WakeAsync(new WakeParams { Text = text, Mode = mode }, ct);

    /// <summary>
    /// 立即注入唤醒文本（mode = now）。
    /// </summary>
    /// <param name="text">注入文本</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示调度是否被接受</returns>
    public Task<GatewayResponse> WakeNowAsync(string text, CancellationToken ct = default)
        => WakeAsync(text, WakeScheduleMode.Now, ct);

    /// <summary>
    /// 在下一次 Agent 心跳时再注入唤醒文本（mode = next-heartbeat）。
    /// </summary>
    /// <param name="text">注入文本</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示调度是否被接受</returns>
    public Task<GatewayResponse> WakeNextHeartbeatAsync(string text, CancellationToken ct = default)
        => WakeAsync(text, WakeScheduleMode.NextHeartbeat, ct);

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
    /// 列出所有已配置的定时任务（无过滤条件）。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含定时任务列表</returns>
    public Task<GatewayResponse> CronListAsync(CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronList, new CronListParams(), ct);

    /// <summary>
    /// 列出定时任务，支持过滤、排序与分页（与 <c>CronListParamsSchema</c> 一致）。
    /// </summary>
    /// <param name="parameters">列表查询参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含定时任务列表</returns>
    public Task<GatewayResponse> CronListAsync(CronListParams parameters, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronList, parameters, ct);

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
    /// 按任务 <c>id</c> 打补丁更新定时任务（请求体字段名为 <c>patch</c>，与 <c>CronUpdateParamsSchema</c> 一致）。
    /// </summary>
    /// <param name="id">任务 id</param>
    /// <param name="patch">部分更新内容（<c>CronJobPatchSchema</c>）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> CronUpdateAsync(string id, object patch, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronUpdate, new { id, patch }, ct);

    /// <summary>
    /// 按 <c>jobId</c> 打补丁更新定时任务（与 <c>id</c> 在协议中为二选一别名）。
    /// </summary>
    /// <param name="jobId">任务 jobId</param>
    /// <param name="patch">部分更新内容</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> CronUpdateByJobIdAsync(string jobId, object patch, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronUpdate, new { jobId, patch }, ct);

    /// <summary>
    /// 按任务 <c>id</c> 移除定时任务。
    /// </summary>
    /// <param name="id">任务 id</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> CronRemoveAsync(string id, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronRemove, new { id }, ct);

    /// <summary>
    /// 按 <c>jobId</c> 移除定时任务。
    /// </summary>
    /// <param name="jobId">任务 jobId</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示操作是否成功</returns>
    public Task<GatewayResponse> CronRemoveByJobIdAsync(string jobId, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronRemove, new { jobId }, ct);

    /// <summary>
    /// 手动触发执行指定定时任务。可选 <paramref name="mode"/>：<c>due</c> 或 <c>force</c>（见 <c>CronRunParamsSchema</c>）。
    /// </summary>
    /// <param name="id">任务 id</param>
    /// <param name="mode">运行模式，省略时由网关默认</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示执行是否成功</returns>
    public Task<GatewayResponse> CronRunAsync(string id, string? mode = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronRun, new { id, mode }, ct);

    /// <summary>
    /// 按 <c>jobId</c> 手动触发执行定时任务。
    /// </summary>
    /// <param name="jobId">任务 jobId</param>
    /// <param name="mode">运行模式</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示执行是否成功</returns>
    public Task<GatewayResponse> CronRunByJobIdAsync(string jobId, string? mode = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronRun, new { jobId, mode }, ct);

    /// <summary>
    /// 查询定时任务执行历史（无额外过滤，与空 <see cref="CronRunsParams"/> 等价）。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含执行记录列表</returns>
    public Task<GatewayResponse> CronRunsAsync(CancellationToken ct = default)
        => CronRunsAsync(new CronRunsParams(), ct);

    /// <summary>
    /// 查询定时任务执行历史（完整参数，与 <c>CronRunsParamsSchema</c> 一致）。
    /// </summary>
    /// <param name="parameters">过滤与分页参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含执行记录列表</returns>
    public Task<GatewayResponse> CronRunsAsync(CronRunsParams parameters, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.CronRuns, parameters, ct);

    /// <summary>
    /// 查询某一任务的执行历史（设置 <see cref="CronRunsParams.Id"/>）。
    /// </summary>
    /// <param name="id">任务 id</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含执行记录列表</returns>
    public Task<GatewayResponse> CronRunsForJobAsync(string id, CancellationToken ct = default)
        => CronRunsAsync(new CronRunsParams { Id = id }, ct);

    /// <summary>
    /// 查询某一任务的执行历史（使用协议别名字段 <c>jobId</c>）。
    /// </summary>
    /// <param name="jobId">任务 jobId</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含执行记录列表</returns>
    public Task<GatewayResponse> CronRunsByJobIdAsync(string jobId, CancellationToken ct = default)
        => CronRunsAsync(new CronRunsParams { JobId = jobId }, ct);

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
    //  Send (Messaging)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 直接出站投递 RPC：在聊天运行器之外，向指定渠道/账号/线程投递消息。
    /// 适用于主动通知、定时推送、跨渠道转发等不经过 chat runner 的场景。
    /// </summary>
    /// <param name="sendParams">强类型发送参数，包含目标渠道、账号、线程及消息内容</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含投递回执（消息 ID、时间戳等）</returns>
    public Task<GatewayResponse> SendAsync(SendParams sendParams, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.Send, sendParams, ct);

    /// <summary>
    /// 直接出站投递 RPC 的便捷重载：向指定渠道和账号发送纯文本消息。
    /// </summary>
    /// <param name="channel">目标渠道标识（如 "telegram"、"discord"）</param>
    /// <param name="account">目标账号/群组标识</param>
    /// <param name="text">消息文本内容</param>
    /// <param name="threadId">目标线程 ID，为 null 时发送到主对话</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，<see cref="GatewayResponse.Payload"/> 包含投递回执</returns>
    public Task<GatewayResponse> SendAsync(string channel, string account, string text, string? threadId = null, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.Send, new SendParams
        {
            Channel = channel,
            Account = account,
            Text = text,
            ThreadId = threadId,
        }, ct);

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

    /// <summary>
    /// 向聊天会话直接注入一条消息，不触发 Agent 回复生成。
    /// 典型用途：插入系统提示、旁白、上下文修正或外部工具返回的内容。
    /// 注入的消息会被持久化到会话历史中，后续 Agent 可将其作为上下文参考。
    /// </summary>
    /// <param name="injectParams">注入参数，包含角色、内容和可选的结构化载荷</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示注入是否成功</returns>
    public Task<GatewayResponse> ChatInjectAsync(ChatInjectParams injectParams, CancellationToken ct = default)
        => InvokeAsync(GatewayConstants.Methods.ChatInject, injectParams, ct);

    /// <summary>
    /// 向聊天会话直接注入一条消息（便捷重载）。
    /// 若未指定 sessionKey，依次使用：服务端下发的主会话键 → <see cref="GatewayConstants.DefaultSessionKey"/>。
    /// </summary>
    /// <param name="role">消息角色（如 "user"、"assistant"、"system"）</param>
    /// <param name="content">消息文本内容</param>
    /// <param name="sessionKey">目标会话键，为 null 时自动选择默认会话</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关响应，表示注入是否成功</returns>
    public Task<GatewayResponse> ChatInjectAsync(string role, string content, string? sessionKey = null, CancellationToken ct = default)
    {
        var key = sessionKey
                  ?? _helloOk?.Snapshot?.SessionDefaults?.MainSessionKey
                  ?? GatewayConstants.DefaultSessionKey;

        return InvokeAsync(GatewayConstants.Methods.ChatInject, new ChatInjectParams
        {
            SessionKey = key,
            Role = role,
            Content = content,
        }, ct);
    }
}