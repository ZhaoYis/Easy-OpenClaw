using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 将 OpenClaw 网关 RPC 与连接状态暴露为 SignalR Hub 方法的基类；连接成功后会加入用户组、档位组与系统广播组。
/// </summary>
public abstract class OpenClawGatewayHubBase : Hub
{
    private readonly IOpenClawGatewayRpc _rpc;
    private readonly IOptions<OpenClawSignalROptions> _options;
    private readonly IOpenClawSignalRConnectionPresenceStore _presenceStore;
    private readonly IOpenClawSignalRGatewayHubBridge _hubBridge;
    private readonly ILogger _logger;

    protected OpenClawGatewayHubBase(
        IOpenClawGatewayRpc rpc,
        IOptions<OpenClawSignalROptions> options,
        IOpenClawSignalRConnectionPresenceStore presenceStore,
        IOpenClawSignalRGatewayHubBridge hubBridge,
        ILoggerFactory loggerFactory)
    {
        _rpc = rpc;
        _options = options;
        _presenceStore = presenceStore;
        _hubBridge = hubBridge;
        _logger = loggerFactory.CreateLogger(GetType());
    }

    /// <summary>在加入默认组之后调用；返回额外要加入的组名（已规范化，可直接使用）。</summary>
    protected virtual IEnumerable<string> GetAdditionalConnectionGroups(OpenClawSignalRConnectionContext context) => [];

    /// <summary>在 RPC 白名单校验之后、发往网关之前调用；可抛出 <see cref="HubException"/> 拒绝调用。</summary>
    protected virtual ValueTask OnBeforeInvokeRpcAsync(string method, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>
    /// 为 true 时拒绝未认证建连（默认）；匿名 Hub 应重写为 false。
    /// </summary>
    protected virtual bool RequireAuthenticatedConnection => true;

    public override async Task OnConnectedAsync()
    {
        var opts = _options.Value;
        var ctx = new OpenClawSignalRConnectionContext(Context.ConnectionId, Context.User, opts);
        var isAuthenticated = Context.User?.Identity?.IsAuthenticated == true;
        if (RequireAuthenticatedConnection && !isAuthenticated)
        {
            _logger.LogWarning("User is not authenticated（{ConnectionId}）, connection rejected", Context.ConnectionId);
            throw new HubException("User is not authenticated");
        }

        var groups = OpenClawSignalRJoinedGroups.Build(
            Context.User,
            isAuthenticated,
            opts,
            GetAdditionalConnectionGroups(ctx));

        foreach (var g in groups)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, g).ConfigureAwait(false);
        }

        var snapshotUserId = OpenClawSignalRClaimResolution.GetUserId(Context.User, opts.UserIdClaimType);
        var tier = OpenClawSignalRJoinedGroups.ResolveTierForSnapshot(Context.User, opts);
        // 断开时 ConnectionAborted 常已处于取消状态，故注册/移除使用 None，避免因 token 取消导致未能清理运营快照。
        await _presenceStore.RegisterAsync(new OpenClawSignalRConnectionSnapshot(
            Context.ConnectionId,
            snapshotUserId,
            tier,
            DateTimeOffset.UtcNow,
            groups,
            OpenClawSignalRPrincipalSnapshot.From(Context.User)), CancellationToken.None).ConfigureAwait(false);

        await _hubBridge.OnHubConnectedAsync(new OpenClawSignalRGatewayHubBridgeContext(
                Context.ConnectionId,
                snapshotUserId),
            Context.ConnectionAborted).ConfigureAwait(false);

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var disconnectUserId = OpenClawSignalRClaimResolution.GetUserId(Context.User, _options.Value.UserIdClaimType);
        await _presenceStore.RemoveAsync(
            Context.ConnectionId,
            disconnectUserId,
            CancellationToken.None).ConfigureAwait(false);

        await _hubBridge.OnHubDisconnectedAsync(new OpenClawSignalRGatewayHubBridgeContext(
                Context.ConnectionId,
                disconnectUserId),
            CancellationToken.None).ConfigureAwait(false);

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>
    /// 代理调用网关 RPC。受 <see cref="OpenClawSignalROptions.AllowedRpcMethods"/> 约束（非空时为白名单）。
    /// </summary>
    /// <remarks>
    /// 不含 <see cref="CancellationToken"/> 参数，以便 SignalR 客户端只需传入 <c>method</c> 与 <c>parameters</c>；
    /// 取消使用 <see cref="Hub.Context"/> 的 <see cref="HubCallerContext.ConnectionAborted"/>。
    /// </remarks>
    public virtual async Task<GatewayResponse> InvokeRpcAsync(string method, JsonElement? parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        var cancellationToken = Context.ConnectionAborted;

        var allow = _options.Value.AllowedRpcMethods;
        if (allow is { Length: > 0 })
        {
            var allowed = allow.Any(m => string.Equals(m, method, StringComparison.Ordinal));

            if (!allowed)
            {
                _logger.LogWarning("Hub RPC denied (not in allowlist): {Method} from {ConnectionId}", method,
                    Context.ConnectionId);
                throw new HubException($"RPC method not allowed: {method}");
            }
        }

        await OnBeforeInvokeRpcAsync(method, cancellationToken).ConfigureAwait(false);

        if (!_rpc.IsConnected)
            throw new HubException("Gateway is not connected.");

        return await _rpc.InvokeAsync(method, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>返回当前网关连接摘要，供前端展示或探测能力列表。</summary>
    public virtual Task<OpenClawGatewayStateDto> GetGatewayStateAsync()
    {
        Context.ConnectionAborted.ThrowIfCancellationRequested();
        var dto = new OpenClawGatewayStateDto(
            _rpc.IsConnected,
            _rpc.State,
            _rpc.AvailableMethods,
            _rpc.AvailableEvents);
        return Task.FromResult(dto);
    }
}