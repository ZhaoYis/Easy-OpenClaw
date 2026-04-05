namespace OpenClaw.Core.Models;

/// <summary>
/// 网关协议中使用的常量定义。
/// </summary>
public static class GatewayConstants
{
    public const string DefaultClientVersion = "2026.3.13";
    public const string DefaultSessionKey = "agent:main:main";
    public const string DefaultGatewayUrl = "ws://localhost:18789";

    /// <summary>
    /// 客户端标识符（GATEWAY_CLIENT_IDS）
    /// </summary>
    public static class ClientIds
    {
        public const string WebchatUi = "webchat-ui";
        public const string ControlUi = "openclaw-control-ui";
        public const string Tui = "openclaw-tui";
        public const string Webchat = "webchat";
        public const string Cli = "cli";
        public const string GatewayClient = "gateway-client";
        public const string MacosApp = "openclaw-macos";
        public const string IosApp = "openclaw-ios";
        public const string AndroidApp = "openclaw-android";
        public const string NodeHost = "node-host";
        public const string Test = "test";
        public const string Fingerprint = "fingerprint";
        public const string Probe = "openclaw-probe";
    }

    /// <summary>
    /// 客户端模式（GATEWAY_CLIENT_MODES）
    /// </summary>
    public static class ClientModes
    {
        public const string Webchat = "webchat";
        public const string Cli = "cli";
        public const string Ui = "ui";
        public const string Backend = "backend";
        public const string Node = "node";
        public const string Probe = "probe";
        public const string Test = "test";
    }

    /// <summary>
    /// 角色定义
    /// </summary>
    public static class Roles
    {
        /// <summary>控制平面的客户端（CLI/UI/自动化脚本）</summary>
        public const string Operator = "operator";
        /// <summary>节点能力提供者（机器人、设备等能力宿主）</summary>
        public const string Node = "node";
    }

    /// <summary>
    /// 权限作用域（Scopes）
    /// </summary>
    public static class Scopes
    {
        public const string Admin = "operator.admin";
        public const string Read = "operator.read";
        public const string Write = "operator.write";
        public const string Approvals = "operator.approvals";
        public const string Pairing = "operator.pairing";
        public const string TalkSecrets = "operator.talk.secrets";
    }
}
