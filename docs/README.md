# Easy-OpenClaw

面向 [OpenClaw](https://openclaw.ai) Gateway 的 **.NET 10** 客户端：WebSocket 传输、Ed25519 设备认证、RPC 与事件订阅；可选 *
*ASP.NET Core SignalR** 把网关能力暴露给浏览器/移动端。附带 CLI 聊天客户端与设备配对自动审批示例。

**环境**：已安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)，且可访问运行中的 OpenClaw Gateway。

**扩展阅读**：[OpenClaw 事件与 RPC 集成](openclaw-events-and-rpc.md)（方法名/事件名、订阅方式、SignalR 侧约定）。

---

## 仓库结构

| 项目                                                          | 用途                                                                  |
|-------------------------------------------------------------|---------------------------------------------------------------------|
| `OpenClaw.Core`                                             | SDK：`GatewayClient`、事件、`AddOpenClaw` / `UseOpenClawEventSubscriber` |
| `OpenClaw.Core.SignalR`                                     | Hub 基类、JWT、RPC 透传、网关事件 → SignalR、连接运营存储                             |
| `OpenClaw.Gateway.Client`                                   | 交互式 CLI                                                             |
| `OpenClaw.AutoApprove.Core` / `OpenClaw.AutoApprove.Client` | 轮询 `device.pair.list` 并自动 `approve`                                 |
| `tests/*`                                                   | 单元测试与 SignalR 集成测试                                                  |

---

## 本地运行示例程序

配置节 **`OpenClaw`**（与 `GatewayOptions.SectionName` 一致），示例见各项目的 `appsettings.json`：

```json
{
  "OpenClaw": {
    "Url": "ws://localhost:18789",
    "Token": "YOUR_GATEWAY_TOKEN"
  }
}
```

- CLI：`dotnet run --project OpenClaw.Gateway.Client`
- 自动审批：`dotnet run --project OpenClaw.AutoApprove.Client`（该进程本身需先在网关侧完成一次配对）

环境变量覆盖：`OpenClaw__Url`、`OpenClaw__Token` 等（ASP.NET Core 双下划线嵌套）。  
设备状态目录：未配置时默认 `~/.openclaw-client`，可用环境变量 **`OPENCLAW_STATE_DIR`** 指定。

自动审批轮询间隔：配置节 **`AutoApprove`**（`AutoApproveOptions.SectionName`）。

---

## 对接方式一：直接引用 `OpenClaw.Core`

1. 项目引用 `OpenClaw.Core`。
2. 绑定配置或使用 `GatewayOptions` 手动构造 `GatewayClient`。
3. `ConnectWithRetryAsync()` 后调用 `GatewayClient` 上的 RPC 方法（方法集见 `GatewayClient.Methods.cs`）；事件用 `OnEvent`
   或 DI 中的事件订阅扩展。

**DI 示例**：

```csharp
services.AddOpenClaw(configuration.GetSection(GatewayOptions.SectionName));
services.UseOpenClawEventSubscriber();
```

**手动示例**：

```csharp
await using var client = new GatewayClient(new GatewayOptions
{
    Url = "ws://localhost:18789",
    Token = "your-token",
    KeyFilePath = "/path/to/device.key",
    DeviceTokenFilePath = "/path/to/device.token",
});
await client.ConnectWithRetryAsync();
```

常用配置项：`Url`、`Token`（或 `Password`，视网关认证方式）、`KeyFilePath`、`DeviceTokenFilePath`、`ClientId` / `Role` /
`Scopes`、重连与 RPC 超时等——完整定义见 `OpenClaw.Core/Models/GatewayOptions.cs`。

事件订阅、`SendRequestAsync` 与 SignalR
转暴露的约定见 [/openclaw-events-and-rpc.md](openclaw-events-and-rpc.md)。

---

## 对接方式二：ASP.NET Core + SignalR

引用 `OpenClaw.Core` 与 `OpenClaw.Core.SignalR`，按顺序注册：**OpenClaw → 认证授权 →
SignalR → `AddOpenClawSignalRGateway<THub>` → 必须选择一种连接存储**。

```csharp
builder.Services.AddOpenClaw(builder.Configuration.GetSection(GatewayOptions.SectionName));
builder.Services.AddAuthorization();
builder.Services.AddOpenClawSignalRAuthentication();
builder.Services.AddSignalR();
builder.Services.AddOpenClawSignalRGateway<OpenClawGatewayHub>(
        builder.Configuration.GetSection(OpenClawSignalROptions.SectionName))
    .UseMemoryStore();   // 单机；多实例用 .UseHybridStore(...) 并配置共享 IDistributedCache

// 生产务必注册 IGatewayEventAudienceResolver，否则默认不推送网关事件（防串消息）
// builder.Services.AddSingleton<IGatewayEventAudienceResolver, SystemBroadcastGroupGatewayEventAudienceResolver>();

app.UseAuthentication();
app.UseAuthorization();
app.MapHub<OpenClawGatewayHub>("/hubs/openclaw").RequireAuthorization();
```

**开发/集成测试**可用 `OpenClawGatewayHubAllowAnonymous` 且 Hub 不强制 `RequireAuthorization`（不会加入用户/档位/系统组）。

**移动端 JWT**：WebSocket negotiate 可在 URL 上带 `?access_token=...`，或与 Header `Authorization: Bearer` 等价（由
`AddOpenClawSignalRAuthentication` 处理）；须与 `OpenClawSignalROptions.SignalRHubPathPrefix` 和实际 `MapHub` 路径一致。

**Hub 能力摘要**：

- 客户端可调：`invokeRpcAsync`、`getGatewayStateAsync`（具体名以 JSON 协议 camelCase 为准）。
- 建连后加入组：`oc:user:{id}`、`oc:tier:{档位}`、`oc:system`（匿名 Hub 不适用）。
- 网关下行事件：由 `IGatewayEventAudienceResolver` 决定推送到哪些连接/组；系统级广播用
  `IOpenClawSystemBroadcastSender<THub>`，与网关事件通道分离。
- `OpenClawSignalROptions`：`AllowedRpcMethods`、`EventAllowlist`、`EnableBackgroundConnect`（首个 SignalR 连接出现后是否后台连网关）、JWT
  等——见 `OpenClaw.Core.SignalR/OpenClawSignalROptions.cs`。

运营侧 REST（在线连接查询、按用户/连接/组下发）见 `IOpenClawSignalROperationService` /
`OpenClawSignalROperationControllerBase`（测试与用法可参考 `OpenClaw.Core.SignalR.Tests`）。

`invokeRpcAsync` / 客户端事件名与网关推送的关系见 [docs/openclaw-events-and-rpc.md](docs/openclaw-events-and-rpc.md) 第
5 节。

---

## 与网关协议的约定（升级时对照源码）

本仓库不维护与上游 Gateway **发行标签**的一一映射；握手与 RPC 以代码为准。网关升级后若握手失败或字段不匹配，请同步更新本仓库常量/模型并跑测试。

| 项               | 当前约定                     | 代码位置                                                        |
|-----------------|--------------------------|-------------------------------------------------------------|
| WebSocket 帧协议版本 | `3`                      | `GatewayConstants.Protocol.Version`                         |
| 握手上报的客户端版本      | `2026.4.8`               | `GatewayConstants.DefaultClientVersion`                     |
| 设备签名            | Ed25519，`v2`，`base64url` | `GatewayConstants.Signature.VersionPrefix`，`DeviceIdentity` |

---

## 构建与测试

```bash
dotnet build Easy-OpenClaw.sln
dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj
dotnet test tests/OpenClaw.Core.SignalR.Tests/OpenClaw.Core.SignalR.Tests.csproj
```

---

## 技术栈摘要

传输：`System.Net.WebSockets.ClientWebSocket`；加密：NSec（Ed25519）；序列化：`System.Text.Json`。

## License

MIT
