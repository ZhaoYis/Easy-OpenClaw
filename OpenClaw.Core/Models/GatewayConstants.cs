namespace OpenClaw.Core.Models;

/// <summary>
/// 网关协议中使用的常量定义。
/// </summary>
public static class GatewayConstants
{
    /// <summary>默认客户端版本号</summary>
    public const string DefaultClientVersion = "2026.3.13";

    /// <summary>默认会话键</summary>
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
}
