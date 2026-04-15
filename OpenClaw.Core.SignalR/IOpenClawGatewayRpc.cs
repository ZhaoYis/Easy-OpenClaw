using System.Text.Json;
using OpenClaw.Core.Client;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 对 <see cref="OpenClaw.Core.Client.GatewayClient"/> 的 RPC 与连接状态抽象，便于测试与替换实现。
/// </summary>
public interface IOpenClawGatewayRpc
{
    /// <summary>与底层 <see cref="GatewayClient.IsConnected"/> 一致。</summary>
    bool IsConnected { get; }

    /// <summary>与底层 <see cref="GatewayClient.State"/> 一致。</summary>
    ConnectionState State { get; }

    /// <summary>hello-ok 中声明的 RPC 方法名列表。</summary>
    IReadOnlyList<string> AvailableMethods { get; }

    /// <summary>hello-ok 中声明的可推送事件名列表。</summary>
    IReadOnlyList<string> AvailableEvents { get; }

    /// <summary>
    /// 调用网关 RPC。<paramref name="parameters"/> 为 null 时发送空 JSON 对象 <c>{}</c>。
    /// </summary>
    /// <param name="method">完整方法名，如 <c>health</c>、<c>chat.send</c></param>
    /// <param name="parameters">已构造的 JSON 参数；null 等价于 <c>{}</c></param>
    /// <param name="ct">取消令牌</param>
    Task<GatewayResponse> InvokeAsync(string method, JsonElement? parameters, CancellationToken ct = default);

    /// <summary>
    /// 将强类型 <paramref name="parameters"/> 序列化为 JSON 后调用网关 RPC。
    /// </summary>
    /// <param name="method">RPC 方法名</param>
    /// <param name="parameters">可 JSON 序列化的请求体</param>
    /// <param name="ct">取消令牌</param>
    Task<GatewayResponse> InvokeAsync<T>(string method, T parameters, CancellationToken ct = default);

    /// <summary>
    /// 向网关发送用户消息（<c>chat.send</c>）；<paramref name="sessionKey"/> 为 null 时使用默认会话。
    /// </summary>
    /// <param name="userMessage">用户输入文本</param>
    /// <param name="sessionKey">目标会话键，可选</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>网关对发送请求的响应</returns>
    Task<GatewayResponse> ChatAsync(string userMessage, string? sessionKey = null, CancellationToken ct = default);
}