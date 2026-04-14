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

    public OpenClawGatewayRpc(GatewayClient client)
    {
        _client = client;
    }

    public bool IsConnected => _client.IsConnected;

    public ConnectionState State => _client.State;

    public IReadOnlyList<string> AvailableMethods => _client.AvailableMethods;

    public IReadOnlyList<string> AvailableEvents => _client.AvailableEvents;

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

    public Task<GatewayResponse> InvokeAsync<T>(string method, T parameters, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        return _client.SendRequestAsync<T>(method, parameters, ct);
    }

    public Task<GatewayResponse> ChatAsync(string userMessage, string? sessionKey = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        return _client.ChatAsync(userMessage, sessionKey, ct);
    }
}