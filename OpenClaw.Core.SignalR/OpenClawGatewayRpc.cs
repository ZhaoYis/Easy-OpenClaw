using System.Text.Json;
using OpenClaw.Core.Client;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 默认将 RPC 转发到 DI 中的 <see cref="GatewayClient"/>。
/// </summary>
public sealed class OpenClawGatewayRpc : IOpenClawGatewayRpc
{
    private readonly GatewayClient _client;

    /// <summary>使用共享的 <see cref="GatewayClient"/> 单例转发 RPC。</summary>
    public OpenClawGatewayRpc(GatewayClient client)
    {
        _client = client;
    }

    /// <summary>与底层 <see cref="GatewayClient.IsConnected"/> 一致。</summary>
    public bool IsConnected => _client.IsConnected;

    /// <summary>与底层 <see cref="GatewayClient.State"/> 一致。</summary>
    public ConnectionState State => _client.State;

    /// <summary>hello-ok 中声明的 RPC 方法名列表。</summary>
    public IReadOnlyList<string> AvailableMethods => _client.AvailableMethods;

    /// <summary>hello-ok 中声明的可推送事件名列表。</summary>
    public IReadOnlyList<string> AvailableEvents => _client.AvailableEvents;

    /// <summary>
    /// 调用网关 RPC。<paramref name="parameters"/> 为 null 时发送空 JSON 对象 <c>{}</c>。
    /// </summary>
    /// <param name="method">完整方法名，如 <c>health</c>、<c>chat.send</c></param>
    /// <param name="parameters">已构造的 JSON 参数；null 等价于 <c>{}</c></param>
    /// <param name="ct">取消令牌</param>
    /// <returns>通过 <see cref="GatewayClient.SendRequestAsync(string, JsonElement, CancellationToken)"/> 返回的响应</returns>
    public Task<GatewayResponse> InvokeAsync(string method, JsonElement? parameters, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        if (parameters is null)
        {
            JsonElement defaultParam = JsonSerializer.SerializeToElement(new { }, JsonDefaults.SerializerOptions);
            return _client.SendRequestAsync(method, defaultParam, ct);
        }

        return _client.SendRequestAsync(method, parameters.Value, ct);
    }

    /// <summary>
    /// 将强类型 <paramref name="parameters"/> 序列化为 JSON 后调用网关 RPC。
    /// </summary>
    /// <param name="method">RPC 方法名</param>
    /// <param name="parameters">可 JSON 序列化的请求体</param>
    /// <param name="ct">取消令牌</param>
    /// <typeparam name="T">请求参数类型</typeparam>
    /// <returns><see cref="GatewayClient.SendRequestAsync{T}"/> 的响应</returns>
    public Task<GatewayResponse> InvokeAsync<T>(string method, T parameters, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        return _client.SendRequestAsync<T>(method, parameters, ct);
    }

    /// <summary>
    /// 向网关发送用户消息（<c>chat.send</c>）；<paramref name="sessionKey"/> 为 null 时使用默认会话。
    /// </summary>
    /// <param name="userMessage">用户输入文本</param>
    /// <param name="sessionKey">目标会话键，可选</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关对发送请求的响应</returns>
    public Task<GatewayResponse> ChatAsync(string userMessage, string? sessionKey = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        return _client.ChatAsync(userMessage, sessionKey, ct);
    }
}