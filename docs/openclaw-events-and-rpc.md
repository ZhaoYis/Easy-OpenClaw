# OpenClaw 事件与 RPC 集成说明

本文说明在本仓库中如何调用网关 **RPC（请求/响应）**、如何订阅 **服务端推送事件**，以及经 **SignalR** 转暴露时的约定。权威名称以代码中的 `GatewayConstants` 与握手返回为准。

---

## 1. 概念区分

| 方向 | WebSocket 帧 | 典型用途 |
|------|----------------|----------|
| **RPC** | `type: "req"` / 对应响应 | 主动调用：`health`、`chat.send`、`config.get` 等 |
| **事件** | `type: "event"` | 服务端推送：`agent`、`chat`、`sessions.changed` 等 |

握手成功后，`GatewayClient` 会填充 **`HelloOk`**，可通过 **`AvailableMethods`** / **`AvailableEvents`** 查看当前网关声明的能力；也可用 **`IsRpcMethodAdvertised(string)`** 判断某 RPC 是否在 `features.methods` 中列出（列表为空或尚未握手时返回 `null`，调用方仍可尝试发起 RPC 以兼容旧网关）。

---

## 2. 源码入口（对接时按图索骥）

| 内容 | 位置 |
|------|------|
| RPC 方法名字符串常量 | `OpenClaw.Core/Models/GatewayConstants.cs` → `GatewayConstants.Methods` |
| 推送事件名字符串常量 | 同文件 → `GatewayConstants.Events` |
| 已封装的 RPC（`HealthAsync`、`ChatAsync` 等） | `OpenClaw.Core/Client/GatewayClient.Methods.cs` |
| 任意 RPC（自定义参数） | `GatewayClient.SendRequestAsync<T>` / `SendRequestAsync(method, JsonElement)` |
| 事件注册与分发 | `GatewayClient.OnEvent` → 内部 `EventRouter`（`OpenClaw.Core/Client/EventRouter.cs`） |
| 事件帧模型 | `OpenClaw.Core/Models/GatewayMessages.cs` → `GatewayEvent`（含 `Event`、`Payload`、`Seq` 等） |
| 控制台级强类型事件汇总 | `GatewayEventSubscriber`（`OpenClaw.Core/Client/GatewayEventSubscriber.cs`），DI 用 `UseOpenClawEventSubscriber` |

---

## 3. 调用 RPC（`OpenClaw.Core`）

**推荐**：优先使用 `GatewayClient.Methods.cs` 中与网关一致命名的 `*Async` 方法，避免手写方法名错误。

**需要动态方法名或尚未封装时**：

```csharp
var resp = await client.SendRequestAsync(
    GatewayConstants.Methods.ModelsList,
    new { }, // 或强类型参数对象
    cancellationToken);
// resp.Ok / resp.Payload / resp.Error 为 JsonElement?
```

**注意**：

- 须在 **`ConnectAsync` / `ConnectWithRetryAsync`** 完成且 WebSocket 可用后调用。
- 超时与并发请求由 **`GatewayRequestManager`** + **`GatewayOptions.RequestTimeout`** 管理。

---

## 4. 订阅事件（`OpenClaw.Core`）

**注册**（可多次注册同一事件名，将并行触发；`"*"` 表示所有事件）：

```csharp
client.OnEvent(GatewayConstants.Events.Agent, async evt =>
{
    // evt.Event, evt.Payload, evt.Seq
    await Task.CompletedTask;
});

// 或直接操作路由器（与 OnEvent 等价）
client.Events.On("*", async evt => { ... });
```

**取消某事件名上的全部处理器**：`client.Events.Off(eventName)`。

**序列号**：带 `Seq` 的事件会经 `EventRouter` 做连续性检查，出现跳跃会打警告日志（可能丢包或重连）。

**与 RPC 的配合**：部分推送依赖先发起「订阅类」RPC（例如会话消息、工具流、会话列表变更）。具体方法名见 `GatewayConstants.Methods`（如 `SessionsSubscribe`、`SessionsMessagesSubscribe` 等），并与 `GatewayConstants.Events` 各常量的 XML 注释对照。

**`GatewayEventSubscriber`**：在需要统一把多类事件转成 C# `event` 或日志时使用；注册 `UseOpenClawEventSubscriber` 后按项目内扩展调用 `RegisterAll`（见 `GatewayEventSubscriberExtensions`）。

---

## 5. 通过 SignalR 暴露给前端（`OpenClaw.Core.SignalR`）

服务端 Hub（如 `OpenClawGatewayHubBase` 派生类）提供：

| Hub 方法（C#） | 作用 |
|----------------|------|
| `InvokeRpcAsync(string method, JsonElement? parameters)` | 白名单校验（若配置了 `AllowedRpcMethods`）后转发到网关 |
| `GetGatewayStateAsync()` | 返回是否已连网关、`AvailableMethods`、`AvailableEvents` 等摘要 |

前端（JSON 协议）方法名一般为 **camelCase**，例如配置项 `GatewayEventClientMethod` 默认 `GatewayEvent`、系统广播 `systemBroadcast`，以 `OpenClawSignalROptions` 为准。

**安全与行为**：

- 生产务必配置 **`AllowedRpcMethods`**，限制可调用的网关方法。
- 网关推送转 SignalR 受 **`IGatewayEventAudienceResolver`** 与 **`GatewayEventBroadcastMode`** 约束；默认不解析则**不推送**网关事件（避免串消息）。需按业务注册解析器（如按 `oc:system` 组广播）。
- 可选 **`EventAllowlist`** 限制转发的网关事件名。

Hub 与管道注册顺序、JWT、连接存储等见仓库根目录 [README.md](../README.md) 中「对接方式二」。

---

## 6. 排查建议

1. 先调 **`health`** 或 **`GetGatewayStateAsync`**，确认 RPC 通路。
2. 对照 **`AvailableEvents`** 与 **`GatewayConstants.Events`**，确认事件名拼写。
3. 无推送时检查是否已调用对应 **subscribe** 类 RPC；SignalR 路径还要检查 **受众解析器** 与 **EventAllowlist**。
