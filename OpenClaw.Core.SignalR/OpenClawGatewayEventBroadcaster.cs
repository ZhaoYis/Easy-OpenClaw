using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Client;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 将网关 <see cref="GatewayEvent"/> 经 <see cref="IGatewayEventAudienceResolver"/> 推送到对应 SignalR 连接；
/// 在解析器 <see cref="IGatewayEventAudienceResolver.RequiresConnectionSnapshotEnumeration"/> 为 true 时从
/// <see cref="IOpenClawSignalRConnectionPresenceStore"/> 预取快照并填入 <see cref="GatewayEventAudienceResolveContext"/>。
/// </summary>
public sealed class OpenClawGatewayEventBroadcaster<THub> : IHostedService
    where THub : Hub
{
    private readonly GatewayClient _client;
    private readonly IHubContext<THub> _hubContext;
    private readonly IOptions<OpenClawSignalROptions> _options;
    private readonly IOpenClawSignalRConnectionPresenceStore _presenceStore;
    private readonly IGatewayEventAudienceResolver _audienceResolver;
    private readonly ILogger<OpenClawGatewayEventBroadcaster<THub>> _logger;

    public OpenClawGatewayEventBroadcaster(
        GatewayClient client,
        IHubContext<THub> hubContext,
        IOptions<OpenClawSignalROptions> options,
        IOpenClawSignalRConnectionPresenceStore presenceStore,
        IGatewayEventAudienceResolver audienceResolver,
        ILogger<OpenClawGatewayEventBroadcaster<THub>> logger)
    {
        _client = client;
        _hubContext = hubContext;
        _options = options;
        _presenceStore = presenceStore;
        _audienceResolver = audienceResolver;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _client.OnEvent(GatewayConstants.Events.Wildcard, OnGatewayEventAsync);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task OnGatewayEventAsync(GatewayEvent evt)
    {
        var opts = _options.Value;
        // 仅 None 短路；ResolverOnly 及未来扩展值均走受众解析（解析器决定实际目标）。
        switch (opts.GatewayEventBroadcastMode)
        {
            case GatewayEventBroadcastMode.None:
                return;
            case GatewayEventBroadcastMode.ResolverOnly:
                break;
            default:
                return;
        }

        if (opts.EventAllowlist is { Length: > 0 } list)
        {
            var allowed = list.Any(name => string.Equals(name, evt.Event, StringComparison.Ordinal));
            if (!allowed)
                return;
        }

        IReadOnlyList<OpenClawSignalRConnectionSnapshot>? snapshots = null;
        if (_audienceResolver.RequiresConnectionSnapshotEnumeration)
        {
            snapshots = await _presenceStore.GetSnapshotsAsync(CancellationToken.None).ConfigureAwait(false);
        }

        var resolveContext = new GatewayEventAudienceResolveContext(evt, _hubContext.Clients, opts, snapshots);
        if (!_audienceResolver.TryResolveClients(resolveContext, out var target))
        {
            if (opts.LogUnresolvedGatewayEventAudience)
                _logger.LogDebug("Gateway event {Event} skipped: audience resolver returned no target (seq={Seq})",
                    evt.Event, evt.Seq);
            return;
        }

        try
        {
            await target
                .SendAsync(opts.GatewayEventClientMethod, evt, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push gateway event {Event} to SignalR clients", evt.Event);
        }
    }
}