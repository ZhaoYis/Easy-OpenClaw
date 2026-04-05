using System.Text.Json;
using OpenClaw.Core.Client;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Models;
using OpenClaw.Core.Transport;

namespace OpenClaw.Core.Tests.Support;

/// <summary>
/// 测试用 WebSocket 桩：将 <see cref="GatewayClient"/> 的出站 JSON 立即环回为伪造的 response 帧，
/// 从而在不启动真实网关的情况下驱动请求/响应关联与 <see cref="GatewayClient"/> 的 RPC 封装路径。
/// </summary>
internal class LoopbackWebSocket : WebSocketClient
{
    /// <summary>关联的网关客户端，用于调用 <see cref="GatewayClient.SimulateIncomingJsonForTests"/>。</summary>
    protected readonly GatewayClient Client;

    /// <summary>
    /// 初始化环回 WebSocket；基类需要合法 <see cref="Uri"/>，测试场景下仅作占位。
    /// </summary>
    /// <param name="client">被测网关客户端实例</param>
    public LoopbackWebSocket(GatewayClient client) : base(new Uri("ws://unit.test.local"))
    {
        Client = client;
    }

    /// <summary>记录所有出站 JSON 文本，便于断言请求体。</summary>
    public List<string> SentPayloads { get; } = [];

    /// <summary>始终视为已连接，满足 <see cref="GatewayClient.IsConnected"/> 与 Send 前置条件。</summary>
    public override bool IsConnected => true;

    /// <summary>
    /// 捕获出站 JSON，解析请求 id，并异步注入对应的伪造响应帧。
    /// </summary>
    public override Task SendAsync(string json, CancellationToken ct = default)
    {
        SentPayloads.Add(json);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var id = root.GetProperty("id").GetString() ?? "";
        var responseJson = BuildResponseJson(id, root);
        return Client.SimulateIncomingJsonForTests(responseJson);
    }

    /// <summary>
    /// 子类可覆写以定制响应（成功/失败载荷、错误码等）；默认返回 ok=true 的最小 payload。
    /// </summary>
    /// <param name="requestId">出站请求帧中的 id</param>
    /// <param name="requestRoot">已解析的请求根 JSON</param>
    /// <returns>可传入 <see cref="GatewayClient.SimulateIncomingJsonForTests"/> 的完整 response JSON</returns>
    protected virtual string BuildResponseJson(string requestId, JsonElement requestRoot)
    {
        _ = requestRoot;
        var minimal = new
        {
            type = GatewayConstants.FrameTypes.Response,
            id = requestId,
            ok = true,
            payload = new { ok = true },
        };
        return JsonSerializer.Serialize(minimal, JsonDefaults.SerializerOptions);
    }
}

/// <summary>
/// 对所有 RPC 统一返回「失败 + NOT_PAIRED」错误，用于覆盖 <see cref="GatewayClient"/> 中 connect 失败分支的单元测试替身。
/// </summary>
internal sealed class NotPairedLoopbackWebSocket : LoopbackWebSocket
{
    public NotPairedLoopbackWebSocket(GatewayClient client) : base(client)
    {
    }

    /// <inheritdoc />
    protected override string BuildResponseJson(string requestId, JsonElement requestRoot)
    {
        _ = requestRoot;
        var body = new
        {
            type = GatewayConstants.FrameTypes.Response,
            id = requestId,
            ok = false,
            error = new { message = GatewayConstants.ErrorCodes.NotPaired },
        };
        return JsonSerializer.Serialize(body, JsonDefaults.SerializerOptions);
    }
}

/// <summary>
/// 对 <c>health</c> 等方法返回超长 payload，覆盖 <see cref="GatewayClient"/> 中日志截断分支。
/// </summary>
internal sealed class FatPayloadLoopbackWebSocket : LoopbackWebSocket
{
    public FatPayloadLoopbackWebSocket(GatewayClient client) : base(client)
    {
    }

    /// <inheritdoc />
    protected override string BuildResponseJson(string requestId, JsonElement requestRoot)
    {
        var method = requestRoot.TryGetProperty("method", out var m) ? m.GetString() : "";
        var big = method == GatewayConstants.Methods.Health
            ? new string('x', 400)
            : "small";
        var body = new
        {
            type = GatewayConstants.FrameTypes.Response,
            id = requestId,
            ok = true,
            payload = new { text = big },
        };
        return JsonSerializer.Serialize(body, JsonDefaults.SerializerOptions);
    }
}

/// <summary>
/// 对 RPC 统一返回 ok=false 且错误文本不含 NOT_PAIRED，覆盖 <see cref="GatewayClient"/> Invoke 失败日志分支。
/// </summary>
internal sealed class RpcErrorLoopbackWebSocket : LoopbackWebSocket
{
    public RpcErrorLoopbackWebSocket(GatewayClient client) : base(client)
    {
    }

    /// <inheritdoc />
    protected override string BuildResponseJson(string requestId, JsonElement requestRoot)
    {
        _ = requestRoot;
        var body = new
        {
            type = GatewayConstants.FrameTypes.Response,
            id = requestId,
            ok = false,
            error = new { message = "bad_request" },
        };
        return JsonSerializer.Serialize(body, JsonDefaults.SerializerOptions);
    }
}
