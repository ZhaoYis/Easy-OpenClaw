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

    /// <summary>由派生类构造函数调用，保存 RPC、选项、在线状态与桥接依赖。</summary>
    /// <param name="rpc">网关 RPC 抽象</param>
    /// <param name="options">SignalR 桥接选项</param>
    /// <param name="presenceStore">连接运营快照存储</param>
    /// <param name="hubBridge">首个/最后一个客户端时的网关生命周期桥接</param>
    /// <param name="loggerFactory">按 Hub 具体类型创建日志</param>
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

    /// <summary>
    /// 由基类在 <see cref="OnConnectedAsync"/> 中调用；返回的组名与认证后的用户组、档位组、系统广播组一并传入 <see cref="OpenClawSignalRJoinedGroups.Build"/>（额外组名通常列在最前）。
    /// </summary>
    /// <param name="context">当前连接 id、用户与选项</param>
    protected virtual IEnumerable<string> GetAdditionalConnectionGroups(OpenClawSignalRConnectionContext context) => [];

    /// <summary>
    /// 为 true 时拒绝未认证建连（默认）；匿名 Hub 应重写为 false。
    /// </summary>
    protected virtual bool RequireAuthenticatedConnection => true;

    /// <summary>
    /// 校验认证后加入用户/档位/系统广播等组，注册在线快照并通知 <see cref="IOpenClawSignalRGatewayHubBridge"/>。
    /// </summary>
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

        await _hubBridge
            .OnHubConnectedAsync(new OpenClawSignalRGatewayHubBridgeContext(Context.ConnectionId, snapshotUserId), Context.ConnectionAborted)
            .ConfigureAwait(false);

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <summary>从在线存储移除连接并转发桥接断开通知。</summary>
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
    /// <param name="method">网关 RPC 方法名，与 <c>req.method</c> 一致</param>
    /// <param name="parameters">JSON 参数；null 时等价发送空对象 <c>{}</c></param>
    /// <returns>网关响应帧</returns>
    /// <exception cref="HubException">未连接、方法不在白名单或参数非法</exception>
    /// <remarks>
    /// 不含 <see cref="CancellationToken"/> 参数，以便 SignalR 客户端只需传入 <c>method</c> 与 <c>parameters</c>；
    /// 取消使用 <see cref="Hub.Context"/> 的 <see cref="HubCallerContext.ConnectionAborted"/>。
    /// </remarks>
    public virtual Task<GatewayResponse> InvokeRpcAsync(string method, JsonElement? parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        if (!_rpc.IsConnected)
            throw new HubException("Gateway is not connected.");

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

        return _rpc.InvokeAsync(method, parameters, cancellationToken);
    }

    /// <summary>
    /// 通过已连接的网关 RPC 发送用户消息（等价于客户端 <c>chat.send</c>）。
    /// </summary>
    /// <param name="userMessage">用户输入文本</param>
    /// <param name="sessionKey">会话键；null 时由网关使用默认会话</param>
    /// <returns>网关响应</returns>
    /// <exception cref="HubException">未连接或参数无效</exception>
    public virtual Task<GatewayResponse> ChatAsync(string userMessage, string? sessionKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        if (!_rpc.IsConnected)
            throw new HubException("Gateway is not connected.");

        return _rpc.ChatAsync(userMessage, sessionKey, Context.ConnectionAborted);
    }

    /// <summary>返回当前网关连接摘要，供前端展示或探测能力列表。</summary>
    /// <returns>连接标志、状态及 hello-ok 中的方法与事件列表</returns>
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