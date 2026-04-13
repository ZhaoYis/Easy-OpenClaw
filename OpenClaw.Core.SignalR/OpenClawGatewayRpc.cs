using System.Text.Json;
using Microsoft.Extensions.Options;
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

    public OpenClawGatewayRpc(GatewayClient client)
    {
        _client = client;
    }

    public bool IsConnected => _client.IsConnected;

    public ConnectionState State => _client.State;

    public IReadOnlyList<string> AvailableMethods => _client.AvailableMethods;

    public IReadOnlyList<string> AvailableEvents => _client.AvailableEvents;

    public Task<GatewayResponse> InvokeAsync(string method, JsonElement? parameters, CancellationToken cancellationToken = default)
    {
        if (parameters is null)
            return _client.SendRequestAsync(method, JsonSerializer.SerializeToElement(new { }, JsonDefaults.SerializerOptions),
                cancellationToken);

        return _client.SendRequestAsync(method, parameters.Value, cancellationToken);
    }
}
