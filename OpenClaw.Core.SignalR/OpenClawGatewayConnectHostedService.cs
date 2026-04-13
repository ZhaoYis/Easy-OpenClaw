using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Client;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 可选：应用启动时在后台完成到 OpenClaw 网关的 WebSocket 连接（受 <see cref="OpenClawSignalROptions.EnableBackgroundConnect"/> 控制）。
/// </summary>
public sealed class OpenClawGatewayConnectHostedService : IHostedService
{
    private readonly GatewayClient _client;
    private readonly IOptions<OpenClawSignalROptions> _options;
    private readonly ILogger<OpenClawGatewayConnectHostedService> _logger;

    public OpenClawGatewayConnectHostedService(
        GatewayClient client,
        IOptions<OpenClawSignalROptions> options,
        ILogger<OpenClawGatewayConnectHostedService> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Value.EnableBackgroundConnect)
            return;

        try
        {
            await _client.ConnectWithRetryAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("OpenClaw gateway background connect completed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenClaw gateway background connect failed.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
