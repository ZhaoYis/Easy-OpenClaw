using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Client;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 首个 SignalR 客户端连上后注册网关事件监听；若 <see cref="OpenClawSignalROptions.EnableBackgroundConnect"/> 为 true 则再执行
/// <see cref="GatewayClient.ConnectWithRetryAsync"/>；最后一个客户端断开时取消订阅并断开传输（不释放单例 <see cref="GatewayClient"/> 本身）。
/// </summary>
public sealed class OpenClawSignalRGatewayHubBridgeCoordinator<THub> : IOpenClawSignalRGatewayHubBridge
    where THub : Hub
{
    private readonly GatewayClient _client;
    private readonly IHubContext<THub> _hubContext;
    private readonly IOptions<OpenClawSignalROptions> _options;
    private readonly IOpenClawSignalRConnectionPresenceStore _presenceStore;
    private readonly IGatewayEventAudienceResolver _audienceResolver;
    private readonly ILogger<OpenClawSignalRGatewayHubBridgeCoordinator<THub>> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private int _hubRefCount;
    private bool _wildcardSubscribed;
    private object? _wildcardSubscriptionState;
    private bool _transportConnected;

    /// <summary>注入网关客户端、Hub 上下文、选项、运营存储、受众解析器与日志。</summary>
    public OpenClawSignalRGatewayHubBridgeCoordinator(
        GatewayClient client,
        IHubContext<THub> hubContext,
        IOptions<OpenClawSignalROptions> options,
        IOpenClawSignalRConnectionPresenceStore presenceStore,
        IGatewayEventAudienceResolver audienceResolver,
        ILogger<OpenClawSignalRGatewayHubBridgeCoordinator<THub>> logger)
    {
        _client = client;
        _hubContext = hubContext;
        _options = options;
        _presenceStore = presenceStore;
        _audienceResolver = audienceResolver;
        _logger = logger;
    }

    /// <summary>在 Hub 完成分组与运营快照注册之后调用。</summary>
    /// <param name="context">当前连接 id 与解析出的用户 id</param>
    /// <param name="cancellationToken">取消桥接启动（如连接已中止）</param>
    /// <remarks>首个客户端时注册网关通配符订阅并可选 <see cref="GatewayClient.ConnectWithRetryAsync(object?, CancellationToken)"/>。</remarks>
    public async Task OnHubConnectedAsync(OpenClawSignalRGatewayHubBridgeContext context,
        CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hubRefCount == 0)
                await StartBridgeAsync(context, cancellationToken).ConfigureAwait(false);

            _hubRefCount++;
        }
        catch (Exception)
        {
            if (_hubRefCount == 0)
                await StopBridgeAsync(context, cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>在 Hub 断开流程中调用（建议在移除运营快照之后）。</summary>
    /// <param name="context">与建连时同一连接上下文</param>
    /// <param name="cancellationToken">取消断开清理</param>
    /// <remarks>最后一个客户端时取消订阅并断开网关传输。</remarks>
    public async Task OnHubDisconnectedAsync(OpenClawSignalRGatewayHubBridgeContext context,
        CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hubRefCount == 0)
                return;

            _hubRefCount--;
            if (_hubRefCount == 0)
                await StopBridgeAsync(context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>首个客户端：订阅网关通配符事件并在启用后台连接时 <see cref="GatewayClient.ConnectWithRetryAsync(object?, CancellationToken)"/>。</summary>
    private async Task StartBridgeAsync(OpenClawSignalRGatewayHubBridgeContext context,
        CancellationToken cancellationToken = default)
    {
        _wildcardSubscriptionState = context;
        _client.OnEvent(GatewayConstants.Events.Wildcard, context, OnGatewayEventAsync);
        _wildcardSubscribed = true;

        if (!_options.Value.EnableBackgroundConnect)
        {
            _logger.LogDebug(
                "OpenClaw gateway event subscription attached (first SignalR client ConnectionId={ConnectionId} UserId={UserId})",
                context.ConnectionId, context.UserId ?? "(null)");
            return;
        }

        await _client.ConnectWithRetryAsync(context, cancellationToken).ConfigureAwait(false);
        _transportConnected = true;
        _logger.LogInformation(
            "OpenClaw gateway connected after first SignalR client (ConnectionId={ConnectionId} UserId={UserId}).",
            context.ConnectionId, context.UserId ?? "(null)");
    }

    /// <summary>最后一个客户端：取消通配符订阅并 <see cref="GatewayClient.DisconnectAsync(object?, CancellationToken)"/>。</summary>
    private async Task StopBridgeAsync(OpenClawSignalRGatewayHubBridgeContext context,
        CancellationToken cancellationToken = default)
    {
        if (_wildcardSubscribed)
        {
            _client.Events.Off(GatewayConstants.Events.Wildcard, _wildcardSubscriptionState);
            _wildcardSubscriptionState = null;
            _wildcardSubscribed = false;
        }

        if (_transportConnected)
        {
            try
            {
                await _client.DisconnectAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "OpenClaw gateway disconnect after last SignalR client left (ConnectionId={ConnectionId} UserId={UserId}).",
                    context.ConnectionId, context.UserId ?? "(null)");
            }
            finally
            {
                _transportConnected = false;
            }
        }
    }

    /// <summary>按 <see cref="OpenClawSignalROptions.GatewayEventBroadcastMode"/> 与受众解析器将事件推送到 SignalR。</summary>
    private async Task OnGatewayEventAsync(GatewayEvent evt, object? state)
    {
        var opts = _options.Value;
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

        var resolveContext = new GatewayEventAudienceResolveContext(evt, _hubContext.Clients, opts, snapshots, state);
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