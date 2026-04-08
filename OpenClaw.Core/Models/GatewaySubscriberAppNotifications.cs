namespace OpenClaw.Core.Models;

/// <summary>
/// connect_challenge 事件：连接建立后的挑战参数，用于设备身份校验流程。
/// 作为 <see cref="OpenClaw.Core.Client.GatewayEventSubscriber"/> 向应用层暴露的强类型载荷之一。
/// </summary>
/// <param name="Nonce">服务端下发的随机 nonce</param>
/// <param name="Ts">服务端时间戳字符串</param>
public readonly record struct ConnectChallengeNotification(string Nonce, string Ts);

/// <summary>
/// agent 事件中非「assistant 流式 delta」分支时的摘要（例如其它 stream 类型），便于应用层观测 Agent 通道上的非文本增量。
/// </summary>
/// <param name="Stream">payload 中的 stream 字段，可能为 null</param>
/// <param name="PayloadKeysSummary">payload 顶层属性名的逗号分隔列表，用于快速了解结构</param>
public readonly record struct AgentOtherStreamNotification(string? Stream, string PayloadKeysSummary);

/// <summary>
/// chat 事件：会话状态或 transcript 相关更新（含 pending / streaming / final 等状态）。
/// </summary>
/// <param name="State">聊天状态，如 pending、streaming、final</param>
/// <param name="SessionKey">会话键</param>
/// <param name="Kind">部分网关用 kind 区分子类型</param>
/// <param name="Type">部分网关用 type 区分子类型</param>
public readonly record struct ChatNotification(string? State, string? SessionKey, string? Kind, string? Type);

/// <summary>
/// chat.inject 事件：向 transcript 注入消息等场景。
/// </summary>
public readonly record struct ChatInjectNotification(string? SessionKey, string? Role, string? MessageId);

/// <summary>
/// session.message 事件：订阅会话后的消息新增或更新。
/// </summary>
public readonly record struct SessionMessageNotification(string? SessionKey, string? MessageId, string? Role);

/// <summary>
/// session.tool 事件：工具调用或工具流片段。
/// </summary>
public readonly record struct SessionToolNotification(string? SessionKey, string? ToolCallId, string? ToolName, string? Phase);

/// <summary>
/// sessions.changed 事件：会话列表或元数据变化。
/// </summary>
public readonly record struct SessionsChangedNotification(string? Reason, string? SessionKey);

/// <summary>
/// presence 事件：设备上下线或 presence 变化。
/// </summary>
public readonly record struct PresenceNotification(string? Reason, string? DeviceId, string? Mode, string? Host);

/// <summary>
/// talk_mode 事件：语音对话模式切换（如按键说话与免提）。
/// </summary>
public readonly record struct TalkModeNotification(string? Mode, string? ActiveRaw);

/// <summary>
/// health 事件：网关健康状态快照。
/// </summary>
/// <param name="Ok">整体是否健康，payload 缺失该字段时为 null</param>
/// <param name="ChannelCount">channels 对象顶层属性数量</param>
/// <param name="AgentCount">agents 数组长度</param>
public readonly record struct HealthNotification(bool? Ok, int ChannelCount, int AgentCount);

/// <summary>
/// heartbeat 事件：Agent 心跳。
/// </summary>
public readonly record struct HeartbeatNotification(string? AgentId, string? SessionKey);

/// <summary>
/// cron 事件：定时任务相关推送。
/// </summary>
public readonly record struct CronNotification(string? Action, string? CronId);

/// <summary>
/// node_pair_requested 事件：新节点请求配对。
/// </summary>
public readonly record struct NodePairRequestedNotification(string? RequestId, string? NodeId, string? Label);

/// <summary>
/// node_pair_resolved 事件：节点配对审批结束。
/// </summary>
public readonly record struct NodePairResolvedNotification(string? RequestId, string? Status, string? NodeId);

/// <summary>
/// node_invoke_request 事件：网关向节点下发能力调用。
/// </summary>
public readonly record struct NodeInvokeRequestNotification(string? InvocationId, string? Method, string? NodeId);

/// <summary>
/// device_pair_requested 事件：新设备请求配对。
/// </summary>
public readonly record struct DevicePairRequestedNotification(string? RequestId, string? DeviceId, string? Platform);

/// <summary>
/// device_pair_resolved 事件：设备配对审批结束。
/// </summary>
public readonly record struct DevicePairResolvedNotification(string? RequestId, string? Status, string? DeviceId);

/// <summary>
/// voicewake_changed 事件：语音唤醒配置变更（仅提供 payload 顶层键摘要）。
/// </summary>
/// <param name="PayloadKeysSummary">payload 顶层属性名的逗号分隔列表</param>
public readonly record struct VoicewakeChangedNotification(string PayloadKeysSummary);

/// <summary>
/// exec_approval_requested 事件：需要人工审批的命令执行请求。
/// </summary>
public readonly record struct ExecApprovalRequestedNotification(string? ApprovalId, string? Tool, string? Command);

/// <summary>
/// exec_approval_resolved 事件：执行审批已决。
/// </summary>
public readonly record struct ExecApprovalResolvedNotification(string? ApprovalId, string? Decision);

/// <summary>
/// plugin_approval_requested 事件：插件发起的审批请求。
/// </summary>
public readonly record struct PluginApprovalRequestedNotification(string? ApprovalId, string? Plugin, string? Description);

/// <summary>
/// plugin_approval_resolved 事件：插件审批已决。
/// </summary>
public readonly record struct PluginApprovalResolvedNotification(string? ApprovalId, string? Decision);

/// <summary>
/// update_available 事件：检测到网关或组件可更新版本。
/// </summary>
public readonly record struct UpdateAvailableNotification(string? CurrentVersion, string? LatestVersion, string? Channel);

/// <summary>
/// 通配符捕获到的未知事件名（不在内置注册表中的 event 字段），便于应用层扩展或调试。
/// </summary>
/// <param name="EventName">原始事件名字符串</param>
/// <param name="PayloadPreview">payload 文本截断预览，可能为 null</param>
public readonly record struct UnknownGatewayEventNotification(string EventName, string? PayloadPreview);
