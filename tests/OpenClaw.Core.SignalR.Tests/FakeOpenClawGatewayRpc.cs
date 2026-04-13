using System.Text.Json;
using OpenClaw.Core.Client;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Models;
using OpenClaw.Core.SignalR;

namespace OpenClaw.Tests.SignalR;

/// <summary>测试用 RPC 后端：可模拟已连接/未连接。</summary>
internal sealed class FakeOpenClawGatewayRpc : IOpenClawGatewayRpc
{
    public FakeOpenClawGatewayRpc(
        bool connected = true,
        ConnectionState state = ConnectionState.Connected,
        IReadOnlyList<string>? methods = null,
        IReadOnlyList<string>? events = null)
    {
        IsConnected = connected;
        State = state;
        AvailableMethods = methods ?? ["health"];
        AvailableEvents = events ?? ["agent"];
    }

    public bool IsConnected { get; }
    public ConnectionState State { get; }
    public IReadOnlyList<string> AvailableMethods { get; }
    public IReadOnlyList<string> AvailableEvents { get; }

    public Task<GatewayResponse> InvokeAsync(string method, JsonElement? parameters, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new { method, echo = true }, JsonDefaults.SerializerOptions);
        return Task.FromResult(new GatewayResponse
        {
            Ok = true,
            Id = "test-id",
            Payload = payload,
        });
    }
}
