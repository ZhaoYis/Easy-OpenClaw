# Easy-OpenClaw

基于 .NET 10 的 [OpenClaw](https://openclaw.ai) Gateway WebSocket 客户端 SDK，提供网关连接、Ed25519 设备认证、RPC 调用与实时事件订阅能力，并附带交互式 CLI 与设备配对自动审批示例。

## 项目结构

```
Easy-OpenClaw/
├── Easy-OpenClaw.sln
├── OpenClaw.Core/                    # 核心 SDK 库
│   ├── Client/
│   │   ├── GatewayClient.cs          # 高层网关客户端（连接、握手、重连）
│   │   ├── GatewayClient.Methods.cs  # 70+ RPC 方法封装
│   │   ├── GatewayEventSubscriber.cs # 事件订阅管理器
│   │   ├── EventRouter.cs            # 事件路由与分发
│   │   ├── GatewayRequestManager.cs  # 请求/响应关联与超时管理
│   │   └── DeviceIdentity.cs         # Ed25519 设备身份与签名
│   ├── Transport/
│   │   └── WebSocketClient.cs        # 底层 WebSocket 传输层
│   ├── Extensions/
│   │   └── ServiceCollectionExtensions.cs  # DI：AddOpenClaw / UseOpenClawEventSubscriber
│   ├── Models/                       # 协议模型与常量定义
│   └── Logging/
│       └── Log.cs                    # 彩色控制台日志（支持对话流式输出）
├── OpenClaw.Gateway.Client/          # 交互式 CLI 聊天客户端
├── OpenClaw.AutoApprove.Core/        # 自动审批逻辑（类库）
├── OpenClaw.AutoApprove.Client/     # 自动审批可执行程序
└── tests/OpenClaw.Core.Tests/        # 单元测试
```

## 功能特性

### OpenClaw.Core — 核心 SDK

- **WebSocket 传输**：全双工通信，自动心跳保活
- **Ed25519 设备认证**：自动生成/持久化密钥对，v2 签名协议，`base64url` 编码
- **请求/响应关联**：基于 UUID 的请求追踪，可配置超时
- **事件路由系统**：支持精确匹配与通配符 `*` 订阅，序列号间隙检测
- **自动重连**：指数退避重连，`NOT_PAIRED` 自动轮询等待审批
- **70+ RPC 方法**：覆盖 Health、Chat、TTS、Config、Agents、Sessions、Cron、Node、Device Pairing 等网关接口
- **依赖注入**：`AddOpenClaw` 注册 `GatewayClient`，`UseOpenClawEventSubscriber` 注册事件订阅器

### OpenClaw.Gateway.Client — 交互式客户端

- 连接网关并自动完成设备认证握手
- 探测并展示可用 RPC 方法与事件
- 交互式 REPL 聊天，实时流式输出 AI 回复
- 事件监听与日志输出

### OpenClaw.AutoApprove — 自动审批

- **OpenClaw.AutoApprove.Core**：轮询 `device.pair.list`，去重后调用 `device.pair.approve`
- **OpenClaw.AutoApprove.Client**：控制台宿主，通过 `appsettings.json` 配置网关与轮询间隔

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 运行中的 OpenClaw Gateway 实例

## 快速开始

### 1. 克隆项目

```bash
git clone https://github.com/ZhaoYis/Easy-OpenClaw.git
cd Easy-OpenClaw
```

### 2. 配置网关连接

应用使用配置节 **`OpenClaw`**（与 `GatewayOptions.SectionName` 一致）。编辑 `OpenClaw.Gateway.Client/appsettings.json` 或 `OpenClaw.AutoApprove.Client/appsettings.json`：

```json
{
  "OpenClaw": {
    "Url": "ws://localhost:18789",
    "Token": "YOUR_GATEWAY_TOKEN"
  }
}
```

自动审批客户端还可配置轮询间隔（节名 **`AutoApprove`**，与 `AutoApproveOptions.SectionName` 一致）：

```json
{
  "OpenClaw": {
    "Url": "ws://localhost:18789",
    "Token": "YOUR_GATEWAY_TOKEN",
    "ClientId": "cli",
    "ClientMode": "cli",
    "Role": "operator",
    "Scopes": [ "operator.pairing", "operator.read" ]
  },
  "AutoApprove": {
    "PollIntervalSeconds": 2
  }
}
```

也可使用 ASP.NET Core 环境变量约定覆盖配置（双下划线表示嵌套）：

```bash
export OpenClaw__Url="ws://localhost:18789"
export OpenClaw__Token="your-token-here"
export AutoApprove__PollIntervalSeconds="2"
```

CLI 与自动审批程序会将设备密钥与 DeviceToken 默认写入用户目录下的状态目录；可通过 **`OPENCLAW_STATE_DIR`** 指定目录（未设置时默认为 `~/.openclaw-client`）。

### 3. 运行交互式客户端

```bash
dotnet run --project OpenClaw.Gateway.Client
```

连接成功后进入交互式聊天界面，输入消息即可与 AI 对话，输入 `/quit` 或按 `Ctrl+C` 退出。

### 4. 运行自动审批服务

```bash
dotnet run --project OpenClaw.AutoApprove.Client
```

> **注意**：自动审批服务自身需要先在 Gateway 控制面板中被手动批准配对，之后才能自动批准其他设备的配对请求。

## 开发与测试

```bash
dotnet build Easy-OpenClaw.sln
dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj
```

## 作为 SDK 使用

将 `OpenClaw.Core` 作为项目引用，即可在自有应用中使用网关能力。也可通过 `Microsoft.Extensions.DependencyInjection` 注册：

```csharp
using OpenClaw.Core.Client;
using OpenClaw.Core.Extensions;
using OpenClaw.Core.Models;

// 方式一：手动构造
var options = new GatewayOptions
{
    Url = "ws://localhost:18789",
    Token = "your-token",
    KeyFilePath = "/path/to/device.key",
    DeviceTokenFilePath = "/path/to/device.token",
};

await using var client = new GatewayClient(options);
await client.ConnectWithRetryAsync();

// 方式二：DI（典型 Web/Worker 宿主）
// services.AddOpenClaw(configuration.GetSection(GatewayOptions.SectionName));
// services.UseOpenClawEventSubscriber();
```

订阅事件与调用 RPC 示例：

```csharp
client.OnEvent("agent", async evt =>
{
    Console.WriteLine($"收到 agent 事件: {evt.Payload}");
});

var health = await client.HealthAsync();
var models = await client.ModelsListAsync();
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

## 配置选项（GatewayOptions）

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `Url` | `ws://localhost:18789` | Gateway WebSocket 地址（通过配置绑定时须显式提供有效值） |
| `Token` | `""` | Gateway 访问令牌（通过配置绑定时须非空） |
| `KeyFilePath` | `null` | Ed25519 设备密钥持久化路径 |
| `DeviceTokenFilePath` | `null` | DeviceToken 持久化路径 |
| `ClientId` | `"cli"` | 客户端标识 |
| `ClientVersion` | `"2026.3.13"`（`GatewayConstants.DefaultClientVersion`） | 客户端版本号 |
| `ClientMode` | `"cli"` | 客户端模式 |
| `Role` | `"operator"` | 连接角色 |
| `Scopes` | `operator.admin`, `operator.approvals`, `operator.pairing` | 权限作用域数组 |
| `ReconnectDelay` | `3s` | 重连间隔 |
| `MaxReconnectAttempts` | `10` | 最大重连次数 |
| `RequestTimeout` | `30s` | RPC 请求超时时间 |
| `PairingRetryDelay` | `3s` | `NOT_PAIRED` 重试初始间隔 |
| `PairingRetryMaxDelay` | `30s` | `NOT_PAIRED` 重试最大间隔（指数退避上限） |
| `MaxPairingRetries` | `0`（无限） | `NOT_PAIRED` 最大重试次数 |

## 技术栈

- **运行时**: .NET 10
- **加密**: [NSec.Cryptography](https://nsec.rocks/) (Ed25519)
- **传输**: `System.Net.WebSockets.ClientWebSocket`
- **序列化**: `System.Text.Json`

## License

MIT
