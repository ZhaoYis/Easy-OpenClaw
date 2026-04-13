using System.Text.Json;
using OpenClaw.Core.Client;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 对 <see cref="OpenClaw.Core.Client.GatewayClient"/> 的 RPC 与连接状态抽象，便于测试与替换实现。
/// </summary>
public interface IOpenClawGatewayRpc
{
    bool IsConnected { get; }

    ConnectionState State { get; }

    IReadOnlyList<string> AvailableMethods { get; }

    IReadOnlyList<string> AvailableEvents { get; }

    /// <summary>
    /// 调用网关 RPC。<paramref name="parameters"/> 为 null 时发送空 JSON 对象 <c>{}</c>。
    /// </summary>
    Task<GatewayResponse> InvokeAsync(string method, JsonElement? parameters, CancellationToken cancellationToken = default);
}
