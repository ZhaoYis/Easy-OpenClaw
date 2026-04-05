# Easy-OpenClaw

基于 .NET 10 的 [OpenClaw](https://openclaw.ai) Gateway WebSocket 客户端 SDK，提供完整的网关连接、Ed25519 设备认证、RPC 调用和实时事件订阅能力。

## 项目结构

```
Easy-OpenClaw/
├── OpenClaw.Core/                  # 核心 SDK 库
│   ├── Client/
│   │   ├── GatewayClient.cs        # 高层网关客户端（连接、握手、重连）
│   │   ├── GatewayClient.Methods.cs# 70+ RPC 方法封装
│   │   ├── GatewayEventSubscriber.cs# 事件订阅管理器
│   │   ├── EventRouter.cs          # 事件路由与分发
│   │   ├── RequestManager.cs       # 请求/响应关联与超时管理
│   │   └── DeviceIdentity.cs       # Ed25519 设备身份与签名
│   ├── Transport/
│   │   └── WebSocketClient.cs      # 底层 WebSocket 传输层
│   ├── Models/                     # 协议模型与常量定义
│   └── Logging/
│       └── Log.cs                  # 彩色控制台日志（支持对话流式输出）
├── OpenClaw.Gateway.Client/        # 交互式 CLI 聊天客户端
└── OpenClaw.AutoApprove/           # 设备配对自动审批服务
```

## 功能特性

### OpenClaw.Core — 核心 SDK

- **WebSocket 传输**：全双工通信，自动心跳保活
- **Ed25519 设备认证**：自动生成/持久化密钥对，v2 签名协议，`base64url` 编码
- **请求/响应关联**：基于 UUID 的请求追踪，可配置超时
- **事件路由系统**：支持精确匹配与通配符 `*` 订阅，序列号间隙检测
- **自动重连**：指数退避重连，NOT_PAIRED 自动轮询等待审批
- **70+ RPC 方法**：覆盖 Health、Chat、TTS、Config、Agents、Sessions、Cron、Node、Device Pairing 等全部网关接口

### OpenClaw.Gateway.Client — 交互式客户端

- 连接网关并自动完成设备认证握手
- 探测并展示所有可用 RPC 方法与事件
- 交互式 REPL 聊天，实时流式输出 AI 回复
- 完整的事件监听与日志输出

### OpenClaw.AutoApprove — 自动审批服务

- 后台轮询 `device.pair.list` 接口
- 自动批准所有待处理的设备配对请求
- 请求去重，避免重复审批
- 可配置轮询间隔

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 运行中的 OpenClaw Gateway 实例

## 快速开始

### 1. 克隆项目

```bash
git clone https://github.com/your-username/Easy-OpenClaw.git
cd Easy-OpenClaw
```

### 2. 配置网关连接

编辑对应项目的 `appsettings.json`：

```json
{
  "gatewayUrl": "ws://localhost:18789",
  "gatewayToken": "YOUR_GATEWAY_TOKEN"
}
```

也可通过环境变量覆盖：

```bash
export OPENCLAW_GATEWAY_URL="ws://localhost:18789"
export OPENCLAW_GATEWAY_TOKEN="your-token-here"
```

### 3. 运行交互式客户端

```bash
dotnet run --project OpenClaw.Gateway.Client
```

连接成功后进入交互式聊天界面，输入消息即可与 AI 对话，输入 `/quit` 或按 `Ctrl+C` 退出。

### 4. 运行自动审批服务

```bash
dotnet run --project OpenClaw.AutoApprove
```

> **注意**：自动审批服务自身需要先在 Gateway 控制面板中被手动批准配对，之后才能自动批准其他设备的配对请求。

## 作为 SDK 使用

将 `OpenClaw.Core` 作为项目引用，即可在自己的应用中使用完整的网关能力：

```csharp
using OpenClaw.Core.Client;
using OpenClaw.Core.Models;

var options = new GatewayOptions
{
    Url = "ws://localhost:18789",
    Token = "your-token",
    KeyFilePath = "/path/to/device.key",
    DeviceTokenFilePath = "/path/to/device.token",
};

await using var client = new GatewayClient(options);

// 连接（自动处理 challenge 签名与设备认证）
await client.ConnectWithRetryAsync();

// 订阅事件
client.OnEvent("agent", async evt => {
    Console.WriteLine($"收到 agent 事件: {evt.Payload}");
});

// 调用 RPC
var health = await client.HealthAsync();
var models = await client.ModelsListAsync();

// 发送聊天消息
var resp = await client.ChatAsync("你好！");
```

## 支持的 RPC 方法

| 分类 | 方法 |
|------|------|
| Health & Status | `health`, `status`, `doctor.memory.status`, `logs.tail`, `usage.status`, `usage.cost` |
| Chat | `chat.send`, `chat.history`, `chat.abort` |
| TTS | `tts.status`, `tts.providers`, `tts.enable`, `tts.disable`, `tts.convert`, `tts.setProvider` |
| Config | `config.get`, `config.set`, `config.apply`, `config.patch`, `config.schema`, `config.schema.lookup` |
| Agents | `agents.list`, `agents.create`, `agents.update`, `agents.delete`, `agents.files.*` |
| Sessions | `sessions.list`, `sessions.preview`, `sessions.patch`, `sessions.reset`, `sessions.delete`, `sessions.compact` |
| Models & Tools | `models.list`, `tools.catalog` |
| Device Pairing | `device.pair.list`, `device.pair.approve`, `device.pair.reject`, `device.pair.remove`, `device.token.*` |
| Node | `node.list`, `node.describe`, `node.rename`, `node.pair.*`, `node.pending.*`, `node.invoke.*` |
| Exec Approvals | `exec.approvals.get`, `exec.approvals.set`, `exec.approval.request`, `exec.approval.resolve` |
| Cron | `cron.list`, `cron.status`, `cron.add`, `cron.update`, `cron.remove`, `cron.run`, `cron.runs` |
| Skills | `skills.status`, `skills.bins`, `skills.install`, `skills.update` |
| Others | `wake`, `send`, `secrets.reload`, `secrets.resolve`, `voicewake.*`, `talk.*`, `update.run` |

## 配置选项

`GatewayOptions` 支持以下参数：

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `Url` | — | Gateway WebSocket 地址（必填） |
| `Token` | — | Gateway 访问令牌（必填） |
| `KeyFilePath` | `null` | Ed25519 设备密钥持久化路径 |
| `DeviceTokenFilePath` | `null` | DeviceToken 持久化路径 |
| `ClientId` | `"cli"` | 客户端标识 |
| `ClientMode` | `"cli"` | 客户端模式 |
| `Role` | `"operator"` | 连接角色 |
| `Scopes` | `admin, approvals, pairing` | 权限作用域 |
| `ReconnectDelay` | `3s` | 重连间隔 |
| `MaxReconnectAttempts` | `10` | 最大重连次数 |
| `RequestTimeout` | `30s` | RPC 请求超时时间 |
| `PairingRetryDelay` | `3s` | NOT_PAIRED 重试初始间隔 |
| `PairingRetryMaxDelay` | `30s` | NOT_PAIRED 重试最大间隔（指数退避） |
| `MaxPairingRetries` | `0`（无限） | NOT_PAIRED 最大重试次数 |

## 技术栈

- **运行时**: .NET 10
- **加密**: [NSec.Cryptography](https://nsec.rocks/) (Ed25519)
- **传输**: `System.Net.WebSockets.ClientWebSocket`
- **序列化**: `System.Text.Json`

## License

MIT
