using Microsoft.Extensions.Options;
using OpenClaw.Core.Client;
using OpenClaw.Core.Models;
using OpenClaw.Core.Transport;

namespace OpenClaw.Core.Tests.Support;

/// <summary>
/// 构造带内存设备密钥、可注入环回 WebSocket 的 <see cref="GatewayClient"/>，供各测试复用。
/// </summary>
internal static class GatewayClientTestFactory
{
    /// <summary>
    /// 使用可选配置创建客户端；默认 Token 非空以满足一般 RPC 路径，密钥使用临时内存密钥。
    /// </summary>
    /// <param name="configure">对 <see cref="GatewayOptions"/> 的原地修改，可为 null</param>
    /// <returns>选项包装、请求管理器、事件总线、设备身份与客户端实例</returns>
    public static (
        IOptions<GatewayOptions> Options,
        GatewayRequestManager Requests,
        EventRouter Events,
        DeviceIdentity Device,
        GatewayClient Client) CreateCore(Action<GatewayOptions>? configure = null)
    {
        var opts = new GatewayOptions
        {
            Url = "ws://unit.test",
            Token = "test-token",
            KeyFilePath = null,
        };
        configure?.Invoke(opts);
        var options = Options.Create(opts);
        var requests = new GatewayRequestManager(options);
        var events = new EventRouter();
        var device = DeviceIdentity.LoadOrCreate(null);
        var stateStore = new FileGatewayClientStateStore();
        var connectionOverrides = new DefaultGatewayClientConnectionResolver();
        var client = new GatewayClient(options, requests, events, device, stateStore, connectionOverrides);
        return (options, requests, events, device, client);
    }

    /// <summary>
    /// 创建客户端并挂载指定 <see cref="WebSocketClient"/> 桩（通常为 <see cref="LoopbackWebSocket"/> 子类）。
    /// </summary>
    public static (GatewayClient Client, TSocket Socket) CreateWithSocket<TSocket>(Func<GatewayClient, TSocket> socketFactory, Action<GatewayOptions>? configure = null)
        where TSocket : WebSocketClient
    {
        var (_, _, _, _, client) = CreateCore(configure);
        var socket = socketFactory(client);
        client.AttachWebSocketForTests(socket);
        return (client, socket);
    }
}
