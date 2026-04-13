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
├── OpenClaw.Core.SignalR/            # ASP.NET Core SignalR 桥接（Hub 基类、RPC、事件广播）
├── OpenClaw.Gateway.Client/          # 交互式 CLI 聊天客户端
├── OpenClaw.AutoApprove.Core/        # 自动审批逻辑（类库）
├── OpenClaw.AutoApprove.Client/     # 自动审批可执行程序
└── tests/
    ├── OpenClaw.Core.Tests/          # 核心库单元测试
    └── OpenClaw.Core.SignalR.Tests/  # SignalR 桥接集成测试（Kestrel + HubConnection）
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

### OpenClaw.Core.SignalR — SignalR 桥接

- **`IOpenClawGatewayRpc`**：对 `GatewayClient` 的 RPC 与连接状态抽象，默认实现 `OpenClawGatewayRpc`
- **`OpenClawGatewayHub`**（`[Authorize]` + JWT）/ **`OpenClawGatewayHubAllowAnonymous`**（开发用）/ **`OpenClawGatewayHubBase`**：Hub 方法 `invokeRpcAsync`、`getGatewayStateAsync`；连接后加入 `oc:user:{id}`、`oc:tier:{档位}`、`oc:system` 组；`AllowedRpcMethods` 白名单
- **`AddOpenClawSignalRAuthentication`**：`JwtBearer` + WebSocket 下 query `access_token` 与 Header Bearer
- **`IGatewayEventAudienceResolver`**：通过 **`GatewayEventAudienceResolveContext`** 将每条 `GatewayEvent` 解析为 `IClientProxy`（默认 **零广播** 防串消息）。推荐注册 **`SystemBroadcastGroupGatewayEventAudienceResolver`**（推送到建连时加入的 `oc:system`，与系统广播组一致）；可选 **`AllPresenceConnectionsGatewayEventAudienceResolver`**（按运营快照扇出全部连接，成本较高）；开发可注册 **`AllClientsGatewayEventAudienceResolver`**（全员，高风险）
- **`IOpenClawSystemBroadcastSender<THub>`**：经 **`oc:system`** 组推送 **`systemBroadcast`**，与网关事件通道分离
- **`OpenClawGatewayEventBroadcaster<THub>`**：按解析器定向推送网关事件
- **`OpenClawGatewayConnectHostedService`**：配置 `EnableBackgroundConnect` 后在启动时执行 `ConnectWithRetryAsync`
- **连接运营存储（插件式）**：`AddOpenClawSignalRGateway<THub>(...)` 返回构建器后须调用 **`UseMemoryConnectionPresence()`**（单机内存）、**`UseHybridConnectionPresence(...)`**（共享 `IDistributedCache` + `HybridCache`，可在回调里注册 Redis 等），或 **`UseCustomConnectionPresence` / `UseConnectionPresenceStore<T>`** 接入自定义 `IOpenClawSignalRConnectionPresenceStore`

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

## 与上游 OpenClaw 网关版本对应关系

本仓库**不**单独维护与 OpenClaw Gateway **发行版号**（如 npm/Git tag）的一一映射表；与上游的契约对齐以源码中的常量和握手行为为准。部署时请使用与下列约定**同期或兼容**的 Gateway 构建；若网关升级后出现握手失败、RPC 校验错误或字段不匹配，应同步更新本仓库中的协议常量、模型与 `DefaultClientVersion`，并参考 [OpenClaw](https://openclaw.ai) 官方说明。

| 维度 | 本仓库当前约定 | 代码位置（便于对照与升级） |
|------|----------------|----------------------------|
| 连接握手协议版本 | WebSocket 帧协议 **`3`**（`connect` 请求中 `minProtocol` / `maxProtocol` 均为该值） | `GatewayConstants.Protocol.Version` |
| 客户端版本字符串 | **`2026.3.13`**（握手时随 `connect` 上报，网关可据此做特性判断） | `GatewayConstants.DefaultClientVersion` → `GatewayOptions.ClientVersion` |
| 设备身份签名 | **`v2`** 载荷格式（Ed25519，`base64url`） | `GatewayConstants.Signature.VersionPrefix`，`DeviceIdentity` |
| 能力声明（caps） | 默认包含 **`tool-events`**（工具相关流式事件） | `GatewayConstants.Protocol.CapToolEvents`，`ConnectParams.Caps` |

**维护建议**：升级或更换 Gateway 后，优先在网关侧查看握手返回的 `protocol` / `version` 与错误信息；若上游调整了协议号、客户端版本字符串或 RPC schema，请在本仓库中更新对应常量与 `OpenClaw.Core/Models` 下的参数模型，并运行 `dotnet test` 回归。

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
dotnet test tests/OpenClaw.Core.SignalR.Tests/OpenClaw.Core.SignalR.Tests.csproj
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

### ASP.NET Core：通过 SignalR 暴露网关（JWT + 分组推送）

引用 `OpenClaw.Core.SignalR`，典型移动端顺序：**`AddAuthorization` → `AddOpenClawSignalRAuthentication` → `AddSignalR` → `AddOpenClawSignalRGateway` → 连接存储 `Use…`**，并在管道中启用认证/授权；Hub 使用 **`RequireAuthorization()`**。

```csharp
using OpenClaw.Core.Extensions;
using OpenClaw.Core.SignalR;

builder.Services.AddOpenClaw(builder.Configuration.GetSection(OpenClaw.Core.Models.GatewayOptions.SectionName));
builder.Services.AddAuthorization();
builder.Services.AddOpenClawSignalRAuthentication();
builder.Services.AddSignalR();
builder.Services.AddOpenClawSignalRGateway<OpenClawGatewayHub>(
        builder.Configuration.GetSection(OpenClawSignalROptions.SectionName))
    .UseMemoryConnectionPresence();
// 在 AddOpenClawSignalRGateway 之前注册可替换默认 Null；生产常见：AddSingleton<IGatewayEventAudienceResolver, SystemBroadcastGroupGatewayEventAudienceResolver>()

// 多实例 / 生产可改用 Hybrid，并在回调中注册共享分布式缓存（示例为 Redis；请勿在回调内调用 AddHybridCache，由 UseHybridConnectionPresence 统一注册）：
// builder.Services.AddOpenClawSignalRGateway<OpenClawGatewayHub>(...)
//     .UseHybridConnectionPresence(services =>
//     {
//         services.AddStackExchangeRedisCache(options =>
//         {
//             options.Configuration = "localhost:6379";
//         });
//     });

// …

app.UseAuthentication();
app.UseAuthorization();
app.MapHub<OpenClawGatewayHub>("/hubs/openclaw").RequireAuthorization();
```

**系统广播**（全体已认证在线用户，与网关事件分离）：

```csharp
var sender = app.Services.GetRequiredService<IOpenClawSystemBroadcastSender<OpenClawGatewayHub>>();
await sender.SendAsync(new { kind = "notice", text = "..." });
```

**移动端连接**：WebSocket  negotiate 时在 URL 上携带 `?access_token=...`，或配合客户端 `AccessTokenProvider`；与 Header `Authorization: Bearer` 等价（由 `AddOpenClawSignalRAuthentication` 内 `OnMessageReceived` 处理）。

配置示例（节名 `OpenClawSignalR`）：

```json
{
  "OpenClawSignalR": {
    "GatewayEventClientMethod": "GatewayEvent",
    "SystemBroadcastClientMethod": "systemBroadcast",
    "UserIdClaimType": "sub",
    "TierClaimType": "tier",
    "UserGroupPrefix": "oc:user:",
    "TierGroupPrefix": "oc:tier:",
    "SystemBroadcastGroupName": "oc:system",
    "SignalRHubPathPrefix": "/hubs",
    "GatewayEventBroadcastMode": "ResolverOnly",
    "AllowedRpcMethods": [ "health", "chat.send" ],
    "EventAllowlist": [ "agent", "chat" ],
    "EnableBackgroundConnect": true,
    "Jwt": {
      "Authority": "https://your-idp",
      "Audience": "your-api",
      "RequireHttpsMetadata": true
    }
  }
}
```

对称密钥（例如测试）可使用 `Jwt:SigningKeyBase64` + `Jwt:Issuer` + `Jwt:Audience`，由库写入 `TokenValidationParameters`。

**`IGatewayEventAudienceResolver`**：`GatewayEvent` 本身通常**不含**收件人路由字段；**推荐**让受众与 Hub **建连时的授权与分组**一致，例如注册 **`SystemBroadcastGroupGatewayEventAudienceResolver`**（`context.Clients.Group(SystemBroadcastGroupName)`，与已加入 `oc:system` 的已认证连接一致），或与 **`OpenClawSystemBroadcastSender`** 相同的组语义。需要按连接 id 扇出时可注册 **`AllPresenceConnectionsGatewayEventAudienceResolver`**（解析器将 `RequiresConnectionSnapshotEnumeration` 设为 `true`，广播器会拉取 `IOpenClawSignalRConnectionPresenceStore` 快照填入上下文）。若业务仍能从 `Payload` 推断目标用户，可在自定义解析器中使用 `context.Event.Payload` 并返回 `context.Clients.Group(OpenClawSignalRGroupNames.FormatUserGroup(...))` 等。**禁止在不确定接收者时使用 `Clients.All`**（若确需全员网关事件，可显式注册 **`AllClientsGatewayEventAudienceResolver`** 并自担风险）。默认 **`NullGatewayEventAudienceResolver`** 不推送任何网关事件；未注册自定义解析器时客户端收不到网关推送（RPC 仍可用）。

**开发环境**可使用 **`OpenClawGatewayHubAllowAnonymous`** 且 **`MapHub` 不加 `RequireAuthorization`**；此时不会加入用户/档位/系统组。集成测试曾用 **`AllClientsGatewayEventAudienceResolver`** 模拟旧版全员推送。

**Claim 与组名**：库内对用户 id 依次尝试配置的 `UserIdClaimType`、**`ClaimTypes.NameIdentifier`**、字面 **`sub`**，以兼容 JWT 默认入站 Claim 映射。若仍异常，可在启动时执行 `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()` 后改用短名 Claim。

生产环境请配置 CORS、**`AllowedRpcMethods`**，并自行实现受众解析以保证**消息不串**。

**SignalR 客户端约定**：JSON 协议下服务端调用客户端方法名多为 **camelCase**（如 `gatewayEvent`、`systemBroadcast`、`getGatewayStateAsync`、`invokeRpcAsync`）。Hub 方法不向客户端暴露 `CancellationToken` 参数。

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
