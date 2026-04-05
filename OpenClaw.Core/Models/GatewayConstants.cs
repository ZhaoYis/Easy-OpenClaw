namespace OpenClaw.Core.Models;

/// <summary>
/// 网关协议中使用的常量定义。
/// 涵盖客户端标识、模式、角色、权限、RPC 方法名、事件名、帧类型、协议版本、错误码等。
/// </summary>
public static class GatewayConstants
{
    /// <summary>默认客户端版本号，跟随 Gateway 协议的发布周期更新</summary>
    public const string DefaultClientVersion = "2026.3.13";

    /// <summary>默认会话键，格式为 agent:{agentId}:{session}，指向主 Agent 的主会话</summary>
    public const string DefaultSessionKey = "agent:main:main";

    /// <summary>默认网关 WebSocket 地址</summary>
    public const string DefaultGatewayUrl = "ws://localhost:18789";

    /// <summary>
    /// 客户端标识符（GATEWAY_CLIENT_IDS），用于在网关侧区分不同类型的接入端。
    /// </summary>
    public static class ClientIds
    {
        /// <summary>Web 聊天界面</summary>
        public const string WebchatUi = "webchat-ui";

        /// <summary>OpenClaw 控制台 UI</summary>
        public const string ControlUi = "openclaw-control-ui";

        /// <summary>终端文本界面（TUI）</summary>
        public const string Tui = "openclaw-tui";

        /// <summary>Webchat 后端服务</summary>
        public const string Webchat = "webchat";

        /// <summary>命令行客户端</summary>
        public const string Cli = "cli";

        /// <summary>通用网关客户端</summary>
        public const string GatewayClient = "gateway-client";

        /// <summary>macOS 原生应用</summary>
        public const string MacosApp = "openclaw-macos";

        /// <summary>iOS 原生应用</summary>
        public const string IosApp = "openclaw-ios";

        /// <summary>Android 原生应用</summary>
        public const string AndroidApp = "openclaw-android";

        /// <summary>节点能力宿主</summary>
        public const string NodeHost = "node-host";

        /// <summary>测试用客户端</summary>
        public const string Test = "test";

        /// <summary>设备指纹采集客户端</summary>
        public const string Fingerprint = "fingerprint";

        /// <summary>网关探针（存活检测）</summary>
        public const string Probe = "openclaw-probe";
    }

    /// <summary>
    /// 客户端模式（GATEWAY_CLIENT_MODES），描述客户端的运行形态。
    /// </summary>
    public static class ClientModes
    {
        /// <summary>Web 聊天模式</summary>
        public const string Webchat = "webchat";

        /// <summary>命令行模式</summary>
        public const string Cli = "cli";

        /// <summary>图形界面模式</summary>
        public const string Ui = "ui";

        /// <summary>后端服务模式</summary>
        public const string Backend = "backend";

        /// <summary>节点模式（能力提供者）</summary>
        public const string Node = "node";

        /// <summary>探针模式（存活检测）</summary>
        public const string Probe = "probe";

        /// <summary>测试模式</summary>
        public const string Test = "test";
    }

    /// <summary>
    /// 角色定义，决定连接方在网关中的身份类型。
    /// </summary>
    public static class Roles
    {
        /// <summary>控制平面的客户端（CLI/UI/自动化脚本）</summary>
        public const string Operator = "operator";

        /// <summary>节点能力提供者（机器人、设备等能力宿主）</summary>
        public const string Node = "node";
    }

    /// <summary>
    /// 权限作用域（Scopes），细粒度控制客户端可执行的操作范围。
    /// </summary>
    public static class Scopes
    {
        /// <summary>管理员权限，可访问所有管理接口</summary>
        public const string Admin = "operator.admin";

        /// <summary>只读权限，可查询状态和配置</summary>
        public const string Read = "operator.read";

        /// <summary>读写权限，可修改配置和执行操作</summary>
        public const string Write = "operator.write";

        /// <summary>执行审批权限，可批准/拒绝执行请求</summary>
        public const string Approvals = "operator.approvals";

        /// <summary>设备配对权限，可批准/拒绝设备配对</summary>
        public const string Pairing = "operator.pairing";

        /// <summary>语音对话的密钥访问权限</summary>
        public const string TalkSecrets = "operator.talk.secrets";
    }

    /// <summary>
    /// WebSocket 帧类型标识，用于区分请求帧、响应帧和事件帧。
    /// </summary>
    public static class FrameTypes
    {
        /// <summary>客户端发往网关的 RPC 请求帧</summary>
        public const string Request = "req";

        /// <summary>网关返回的 RPC 响应帧</summary>
        public const string Response = "res";

        /// <summary>网关主动推送的事件帧</summary>
        public const string Event = "event";
    }

    /// <summary>
    /// 网关协议版本与能力声明相关常量。
    /// </summary>
    public static class Protocol
    {
        /// <summary>当前支持的协议版本号（最低/最高均为此值）</summary>
        public const int Version = 3;

        /// <summary>工具事件能力标识，声明客户端支持工具调用的流式事件推送</summary>
        public const string CapToolEvents = "tool-events";
    }

    /// <summary>
    /// 网关 RPC 方法名常量。每个常量对应网关服务端注册的一个 RPC 方法。
    /// 方法名遵循 "domain.action" 的点分命名规范。
    /// </summary>
    public static class Methods
    {
        // ── 连接与握手 ──────────────────────────────────────

        /// <summary>完成网关握手认证，建立有状态连接</summary>
        public const string Connect = "connect";

        // ── 健康与状态 ──────────────────────────────────────

        /// <summary>健康检查</summary>
        public const string Health = "health";

        /// <summary>内存诊断状态</summary>
        public const string DoctorMemoryStatus = "doctor.memory.status";

        /// <summary>拉取最近日志</summary>
        public const string LogsTail = "logs.tail";

        /// <summary>所有渠道的连接状态</summary>
        public const string ChannelsStatus = "channels.status";

        /// <summary>登出指定渠道</summary>
        public const string ChannelsLogout = "channels.logout";

        /// <summary>综合运行状态</summary>
        public const string Status = "status";

        /// <summary>使用量统计</summary>
        public const string UsageStatus = "usage.status";

        /// <summary>使用成本统计</summary>
        public const string UsageCost = "usage.cost";

        // ── TTS 语音合成 ────────────────────────────────────

        /// <summary>TTS 当前状态</summary>
        public const string TtsStatus = "tts.status";

        /// <summary>TTS 可用 Provider 列表</summary>
        public const string TtsProviders = "tts.providers";

        /// <summary>启用 TTS</summary>
        public const string TtsEnable = "tts.enable";

        /// <summary>禁用 TTS</summary>
        public const string TtsDisable = "tts.disable";

        /// <summary>文本转语音</summary>
        public const string TtsConvert = "tts.convert";

        /// <summary>设置 TTS Provider</summary>
        public const string TtsSetProvider = "tts.setProvider";

        // ── 配置管理 ────────────────────────────────────────

        /// <summary>获取配置值</summary>
        public const string ConfigGet = "config.get";

        /// <summary>设置配置值</summary>
        public const string ConfigSet = "config.set";

        /// <summary>应用配置变更</summary>
        public const string ConfigApply = "config.apply";

        /// <summary>批量补丁配置</summary>
        public const string ConfigPatch = "config.patch";

        /// <summary>获取配置 Schema</summary>
        public const string ConfigSchema = "config.schema";

        /// <summary>查找指定路径的 Schema 片段</summary>
        public const string ConfigSchemaLookup = "config.schema.lookup";

        // ── 执行审批 ────────────────────────────────────────

        /// <summary>获取执行审批策略</summary>
        public const string ExecApprovalsGet = "exec.approvals.get";

        /// <summary>设置执行审批策略</summary>
        public const string ExecApprovalsSet = "exec.approvals.set";

        /// <summary>获取节点级执行审批策略</summary>
        public const string ExecApprovalsNodeGet = "exec.approvals.node.get";

        /// <summary>设置节点级执行审批策略</summary>
        public const string ExecApprovalsNodeSet = "exec.approvals.node.set";

        /// <summary>发起执行审批请求</summary>
        public const string ExecApprovalRequest = "exec.approval.request";

        /// <summary>等待执行审批决定</summary>
        public const string ExecApprovalWaitDecision = "exec.approval.waitDecision";

        /// <summary>解决执行审批请求（批准/拒绝）</summary>
        public const string ExecApprovalResolve = "exec.approval.resolve";

        // ── 向导 ────────────────────────────────────────────

        /// <summary>启动向导流程</summary>
        public const string WizardStart = "wizard.start";

        /// <summary>向导下一步</summary>
        public const string WizardNext = "wizard.next";

        /// <summary>取消向导</summary>
        public const string WizardCancel = "wizard.cancel";

        /// <summary>获取向导当前状态</summary>
        public const string WizardStatus = "wizard.status";

        // ── 语音对话 ────────────────────────────────────────

        /// <summary>获取语音对话配置</summary>
        public const string TalkConfig = "talk.config";

        /// <summary>获取/设置语音对话模式</summary>
        public const string TalkMode = "talk.mode";

        // ── 模型与工具 ──────────────────────────────────────

        /// <summary>列出可用的 LLM 模型</summary>
        public const string ModelsList = "models.list";

        /// <summary>获取工具目录</summary>
        public const string ToolsCatalog = "tools.catalog";

        // ── Agent 管理 ──────────────────────────────────────

        /// <summary>列出所有 Agent</summary>
        public const string AgentsList = "agents.list";

        /// <summary>创建新 Agent</summary>
        public const string AgentsCreate = "agents.create";

        /// <summary>更新 Agent 配置</summary>
        public const string AgentsUpdate = "agents.update";

        /// <summary>删除 Agent</summary>
        public const string AgentsDelete = "agents.delete";

        /// <summary>列出 Agent 的文件</summary>
        public const string AgentsFilesList = "agents.files.list";

        /// <summary>获取 Agent 的文件内容</summary>
        public const string AgentsFilesGet = "agents.files.get";

        /// <summary>写入 Agent 的文件内容</summary>
        public const string AgentsFilesSet = "agents.files.set";

        // ── 技能管理 ────────────────────────────────────────

        /// <summary>技能安装状态</summary>
        public const string SkillsStatus = "skills.status";

        /// <summary>可用技能二进制包列表</summary>
        public const string SkillsBins = "skills.bins";

        /// <summary>安装技能</summary>
        public const string SkillsInstall = "skills.install";

        /// <summary>更新技能</summary>
        public const string SkillsUpdate = "skills.update";

        // ── 系统更新 ────────────────────────────────────────

        /// <summary>执行系统更新</summary>
        public const string UpdateRun = "update.run";

        // ── 语音唤醒 ────────────────────────────────────────

        /// <summary>获取语音唤醒配置</summary>
        public const string VoicewakeGet = "voicewake.get";

        /// <summary>设置语音唤醒配置</summary>
        public const string VoicewakeSet = "voicewake.set";

        // ── 密钥管理 ────────────────────────────────────────

        /// <summary>重新加载密钥存储</summary>
        public const string SecretsReload = "secrets.reload";

        /// <summary>解析密钥引用</summary>
        public const string SecretsResolve = "secrets.resolve";

        // ── 会话管理 ────────────────────────────────────────

        /// <summary>列出所有会话</summary>
        public const string SessionsList = "sessions.list";

        /// <summary>预览会话内容</summary>
        public const string SessionsPreview = "sessions.preview";

        /// <summary>补丁修改会话属性</summary>
        public const string SessionsPatch = "sessions.patch";

        /// <summary>重置会话（清空上下文）</summary>
        public const string SessionsReset = "sessions.reset";

        /// <summary>删除会话</summary>
        public const string SessionsDelete = "sessions.delete";

        /// <summary>压缩会话（合并旧消息）</summary>
        public const string SessionsCompact = "sessions.compact";

        // ── 心跳 ────────────────────────────────────────────

        /// <summary>获取最后一次心跳信息</summary>
        public const string LastHeartbeat = "last-heartbeat";

        /// <summary>设置心跳配置</summary>
        public const string SetHeartbeats = "set-heartbeats";

        // ── 唤醒 ────────────────────────────────────────────

        /// <summary>唤醒 Agent</summary>
        public const string Wake = "wake";

        // ── 节点配对 ────────────────────────────────────────

        /// <summary>发起节点配对请求</summary>
        public const string NodePairRequest = "node.pair.request";

        /// <summary>列出节点配对请求</summary>
        public const string NodePairList = "node.pair.list";

        /// <summary>批准节点配对请求</summary>
        public const string NodePairApprove = "node.pair.approve";

        /// <summary>拒绝节点配对请求</summary>
        public const string NodePairReject = "node.pair.reject";

        /// <summary>验证节点配对状态</summary>
        public const string NodePairVerify = "node.pair.verify";

        // ── 设备配对 ────────────────────────────────────────

        /// <summary>列出设备配对请求及已配对设备</summary>
        public const string DevicePairList = "device.pair.list";

        /// <summary>批准设备配对请求</summary>
        public const string DevicePairApprove = "device.pair.approve";

        /// <summary>拒绝设备配对请求</summary>
        public const string DevicePairReject = "device.pair.reject";

        /// <summary>移除已配对设备</summary>
        public const string DevicePairRemove = "device.pair.remove";

        /// <summary>轮换设备令牌</summary>
        public const string DeviceTokenRotate = "device.token.rotate";

        /// <summary>吊销设备令牌</summary>
        public const string DeviceTokenRevoke = "device.token.revoke";

        // ── 节点管理 ────────────────────────────────────────

        /// <summary>重命名节点</summary>
        public const string NodeRename = "node.rename";

        /// <summary>列出所有节点</summary>
        public const string NodeList = "node.list";

        /// <summary>获取节点详细描述</summary>
        public const string NodeDescribe = "node.describe";

        // ── 节点任务与调用 ──────────────────────────────────

        /// <summary>排空节点待处理任务队列</summary>
        public const string NodePendingDrain = "node.pending.drain";

        /// <summary>向节点待处理队列入队任务</summary>
        public const string NodePendingEnqueue = "node.pending.enqueue";

        /// <summary>调用节点能力</summary>
        public const string NodeInvoke = "node.invoke";

        /// <summary>拉取待处理任务</summary>
        public const string NodePendingPull = "node.pending.pull";

        /// <summary>确认任务已消费</summary>
        public const string NodePendingAck = "node.pending.ack";

        /// <summary>提交节点调用结果</summary>
        public const string NodeInvokeResult = "node.invoke.result";

        /// <summary>推送节点事件</summary>
        public const string NodeEvent = "node.event";

        /// <summary>刷新节点 Canvas 能力声明</summary>
        public const string NodeCanvasCapabilityRefresh = "node.canvas.capability.refresh";

        // ── 定时任务 ────────────────────────────────────────

        /// <summary>列出定时任务</summary>
        public const string CronList = "cron.list";

        /// <summary>定时任务运行状态</summary>
        public const string CronStatus = "cron.status";

        /// <summary>添加定时任务</summary>
        public const string CronAdd = "cron.add";

        /// <summary>更新定时任务配置</summary>
        public const string CronUpdate = "cron.update";

        /// <summary>移除定时任务</summary>
        public const string CronRemove = "cron.remove";

        /// <summary>立即运行定时任务</summary>
        public const string CronRun = "cron.run";

        /// <summary>查询定时任务执行记录</summary>
        public const string CronRuns = "cron.runs";

        // ── 网关身份 ────────────────────────────────────────

        /// <summary>获取网关自身身份信息</summary>
        public const string GatewayIdentityGet = "gateway.identity.get";

        // ── 系统级 ──────────────────────────────────────────

        /// <summary>获取在线状态快照</summary>
        public const string SystemPresence = "system-presence";

        /// <summary>推送系统事件</summary>
        public const string SystemEvent = "system-event";

        // ── 消息发送 ────────────────────────────────────────

        /// <summary>通用消息发送</summary>
        public const string Send = "send";

        // ── Agent 操作 ──────────────────────────────────────

        /// <summary>Agent 通用操作</summary>
        public const string Agent = "agent";

        /// <summary>获取 Agent 身份信息</summary>
        public const string AgentIdentityGet = "agent.identity.get";

        /// <summary>等待 Agent 就绪</summary>
        public const string AgentWait = "agent.wait";

        // ── 浏览器 ──────────────────────────────────────────

        /// <summary>发起浏览器操作请求</summary>
        public const string BrowserRequest = "browser.request";

        // ── 聊天 ────────────────────────────────────────────

        /// <summary>获取聊天历史记录</summary>
        public const string ChatHistory = "chat.history";

        /// <summary>中止当前聊天回合</summary>
        public const string ChatAbort = "chat.abort";

        /// <summary>发送聊天消息</summary>
        public const string ChatSend = "chat.send";
    }

    /// <summary>
    /// 网关推送事件名常量。事件名遵循 "domain.action" 的点分命名规范。
    /// 客户端通过 <see cref="Client.EventRouter"/> 订阅事件。
    /// </summary>
    public static class Events
    {
        /// <summary>连接挑战事件，携带 nonce 和时间戳，客户端需据此完成 Ed25519 签名认证</summary>
        public const string ConnectChallenge = "connect.challenge";

        /// <summary>Agent 流式输出事件，携带 delta 文本片段</summary>
        public const string Agent = "agent";

        /// <summary>聊天状态变更事件（pending → streaming → final）</summary>
        public const string Chat = "chat";

        /// <summary>在线状态变更事件（设备上下线）</summary>
        public const string Presence = "presence";

        /// <summary>心跳 tick 事件，定期推送用于保活</summary>
        public const string Tick = "tick";

        /// <summary>语音对话模式变更事件</summary>
        public const string TalkMode = "talk.mode";

        /// <summary>网关关闭事件</summary>
        public const string Shutdown = "shutdown";

        /// <summary>健康状态变更事件</summary>
        public const string Health = "health";

        /// <summary>心跳事件（Agent 级别）</summary>
        public const string Heartbeat = "heartbeat";

        /// <summary>定时任务执行事件</summary>
        public const string Cron = "cron";

        /// <summary>节点配对请求事件（有新节点请求配对）</summary>
        public const string NodePairRequested = "node.pair.requested";

        /// <summary>节点配对结果事件（配对请求被批准/拒绝）</summary>
        public const string NodePairResolved = "node.pair.resolved";

        /// <summary>节点调用请求事件（有新的节点能力调用）</summary>
        public const string NodeInvokeRequest = "node.invoke.request";

        /// <summary>设备配对请求事件（有新设备请求配对）</summary>
        public const string DevicePairRequested = "device.pair.requested";

        /// <summary>设备配对结果事件（配对请求被批准/拒绝）</summary>
        public const string DevicePairResolved = "device.pair.resolved";

        /// <summary>语音唤醒配置变更事件</summary>
        public const string VoicewakeChanged = "voicewake.changed";

        /// <summary>执行审批请求事件（有新的工具执行需要审批）</summary>
        public const string ExecApprovalRequested = "exec.approval.requested";

        /// <summary>执行审批结果事件（审批决定已做出）</summary>
        public const string ExecApprovalResolved = "exec.approval.resolved";

        /// <summary>系统更新可用事件</summary>
        public const string UpdateAvailable = "update.available";

        /// <summary>通配符事件，匹配所有未被特定处理器消费的事件</summary>
        public const string Wildcard = "*";
    }

    /// <summary>
    /// 网关错误码常量，用于识别特定的业务错误。
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>设备尚未完成配对审批，需要在 Gateway 控制面板中手动批准</summary>
        public const string NotPaired = "NOT_PAIRED";
    }

    /// <summary>
    /// 聊天回合的状态标识，由 <see cref="Events.Chat"/> 事件推送。
    /// </summary>
    public static class ChatStates
    {
        /// <summary>等待 Agent 开始处理</summary>
        public const string Pending = "pending";

        /// <summary>Agent 正在流式输出</summary>
        public const string Streaming = "streaming";

        /// <summary>回合完成，Agent 输出已结束</summary>
        public const string Final = "final";
    }

    /// <summary>
    /// Agent 流式事件中的 stream 类型标识。
    /// </summary>
    public static class StreamTypes
    {
        /// <summary>助手输出流，包含 delta 文本片段</summary>
        public const string Assistant = "assistant";
    }

    /// <summary>
    /// 运行平台标识常量，用于连接握手中的 <see cref="ClientInfo.Platform"/> 字段。
    /// </summary>
    public static class Platforms
    {
        /// <summary>.NET 运行时平台标识</summary>
        public const string DotNet = "dotnet";

        /// <summary>macOS 平台标识（兼容浏览器 navigator.platform）</summary>
        public const string MacIntel = "MacIntel";

        /// <summary>Windows 平台标识</summary>
        public const string Win32 = "Win32";

        /// <summary>Linux 平台标识</summary>
        public const string Linux = "Linux";

        /// <summary>未知平台回退值</summary>
        public const string Unknown = "unknown";
    }

    /// <summary>
    /// 默认参数值常量。
    /// </summary>
    public static class Defaults
    {
        /// <summary>默认语言区域设置</summary>
        public const string Locale = "zh";
    }

    /// <summary>
    /// WebSocket 传输层常量。
    /// </summary>
    public static class Transport
    {
        /// <summary>WebSocket 连接时的 Origin 请求头</summary>
        public const string Origin = "http://localhost";

        /// <summary>WebSocket 连接时的默认 User-Agent 请求头</summary>
        public const string DefaultUserAgent = "OpenClaw-CSharp-Client/1.0";

        /// <summary>高层 User-Agent 格式模板，{0}=版本号，{1}=操作系统描述</summary>
        public const string UserAgentTemplate = "OpenClaw-CSharp/{0} ({1})";

        /// <summary>WebSocket 接收缓冲区大小（字节）</summary>
        public const int ReceiveBufferSize = 8192;

        /// <summary>WebSocket KeepAlive 心跳间隔（秒）</summary>
        public const int KeepAliveIntervalSeconds = 15;

        /// <summary>WebSocket 正常关闭时的描述文本</summary>
        public const string CloseDescription = "bye";
    }

    /// <summary>
    /// Ed25519 设备签名相关常量。
    /// </summary>
    public static class Signature
    {
        /// <summary>签名 Payload 版本前缀，当前为 v2 格式</summary>
        public const string VersionPrefix = "v2";
    }

    /// <summary>
    /// 设备状态持久化的默认文件名常量。
    /// </summary>
    public static class FileNames
    {
        /// <summary>Ed25519 私钥种子（hex 编码）的默认文件名</summary>
        public const string DeviceKey = "device.key";

        /// <summary>网关签发的 DeviceToken 的默认文件名</summary>
        public const string DeviceToken = "device.token";
    }
}