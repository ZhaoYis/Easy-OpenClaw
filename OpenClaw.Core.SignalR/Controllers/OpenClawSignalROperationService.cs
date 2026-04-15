using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 运营侧查询在线连接、组分布，并向指定用户、连接或组调用客户端 Hub 方法的默认实现。
/// </summary>
/// <typeparam name="THub">与 <c>MapHub&lt;THub&gt;</c> 一致的 Hub 类型。</typeparam>
/// <remarks>
/// 数据来自注入的 <see cref="IOpenClawSignalRConnectionPresenceStore"/>；发送经 <c>IHubContext&lt;THub&gt;</c>。
/// </remarks>
public sealed class OpenClawSignalROperationService<THub> : IOpenClawSignalROperationService<THub>
    where THub : Hub
{
    private readonly IHubContext<THub> _hubContext;
    private readonly IOpenClawSignalRConnectionPresenceStore _presenceStore;
    private readonly IOptions<OpenClawSignalROptions> _options;

    /// <summary>注入 Hub 上下文、运营快照存储与选项（格式化组名）。</summary>
    public OpenClawSignalROperationService(
        IHubContext<THub> hubContext,
        IOpenClawSignalRConnectionPresenceStore presenceStore,
        IOptions<OpenClawSignalROptions> options)
    {
        _hubContext = hubContext;
        _presenceStore = presenceStore;
        _options = options;
    }

    /// <summary>当前快照副本（按连接）。</summary>
    /// <param name="cancellationToken">取消查询</param>
    public Task<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetOnlineConnectionsAsync(
        CancellationToken cancellationToken = default) =>
        _presenceStore.GetSnapshotsAsync(cancellationToken).AsTask();

    /// <summary>当前连接数。</summary>
    /// <param name="cancellationToken">取消查询</param>
    public async Task<int> GetOnlineConnectionCountAsync(CancellationToken cancellationToken = default)
    {
        var list = await GetOnlineConnectionsAsync(cancellationToken).ConfigureAwait(false);
        return list.Count;
    }

    /// <summary>已认证连接的去重用户 id（匿名连接不计入）。</summary>
    /// <param name="cancellationToken">取消查询</param>
    public async Task<IReadOnlyList<string>> GetDistinctOnlineUserIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _presenceStore.GetSnapshotsAsync(cancellationToken).ConfigureAwait(false);
        return snapshots
            .Select(static s => s.UserId)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>指定用户 id 下的连接（声明 id 与 <see cref="OpenClawSignalROptions.UserIdClaimType"/> 一致）。</summary>
    /// <param name="userId">用户标识</param>
    /// <param name="cancellationToken">取消查询</param>
    public async Task<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetConnectionsForUserAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var snapshots = await _presenceStore.GetSnapshotsAsync(cancellationToken).ConfigureAwait(false);
        return snapshots
            .Where(s => string.Equals(s.UserId, userId, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>当前 <paramref name="user"/> 解析出的用户 id 在运营存储中的连接（无用户 id 声明时返回空列表）。</summary>
    /// <param name="user">通常为 <c>HttpContext.User</c></param>
    /// <param name="cancellationToken">取消查询</param>
    public async Task<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetConnectionsForCurrentUserAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var userId = OpenClawSignalRClaimResolution.GetUserId(user, _options.Value.UserIdClaimType);
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        return await GetConnectionsForUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>统计各 SignalR 组当前覆盖的连接数（同一连接在多个组中会分别计数）。</summary>
    /// <param name="cancellationToken">取消查询</param>
    public async Task<IReadOnlyDictionary<string, int>> GetSignalRGroupConnectionCountsAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _presenceStore.GetSnapshotsAsync(cancellationToken).ConfigureAwait(false);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var snap in snapshots)
        {
            foreach (var g in snap.SignalRGroups)
            {
                counts.TryGetValue(g, out var n);
                counts[g] = n + 1;
            }
        }

        return counts;
    }

    /// <summary>与 Hub 加入的单用户组名一致，便于对组广播。</summary>
    /// <param name="userId">原始用户 id（内部会规范化）</param>
    public string FormatUserGroupName(string userId) =>
        OpenClawSignalRGroupNames.FormatUserGroup(_options.Value, userId);

    /// <summary>与 Hub 加入的档位组名一致。</summary>
    /// <param name="tier">档位 Claim 值</param>
    public string FormatTierGroupName(string tier) =>
        OpenClawSignalRGroupNames.FormatTierGroup(_options.Value, tier);

    /// <summary>向该用户的所有连接调用客户端方法（依赖 <see cref="IUserIdProvider"/>）。</summary>
    /// <param name="userId">与 Claims 中用户 id 一致</param>
    /// <param name="hubMethod">客户端方法名</param>
    /// <param name="args">位置参数，null 等价空数组</param>
    /// <param name="cancellationToken">取消发送</param>
    public Task SendToUserAsync(string userId, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hubMethod);
        return _hubContext.Clients.User(userId).SendCoreAsync(hubMethod, args ?? [], cancellationToken);
    }

    /// <summary>
    /// 根据运营存储中当前用户的 <see cref="OpenClawSignalRConnectionSnapshot.ConnectionId"/> 列表发送（与
    /// <see cref="SendToUserAsync"/> 数据源一致）；无用户 id 或无在线连接时不发送。
    /// </summary>
    /// <param name="user">当前用户主体</param>
    /// <param name="hubMethod">客户端方法名</param>
    /// <param name="args">位置参数</param>
    /// <param name="cancellationToken">取消发送</param>
    public async Task SendToCurrentUserConnectionsAsync(ClaimsPrincipal user, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(hubMethod);
        var connections = await GetConnectionsForCurrentUserAsync(user, cancellationToken).ConfigureAwait(false);
        if (connections.Count == 0)
            return;

        var ids = connections.Select(static s => s.ConnectionId).ToArray();
        await _hubContext.Clients.Clients(ids).SendCoreAsync(hubMethod, args ?? [], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>向指定连接调用客户端方法。</summary>
    /// <param name="connectionId">SignalR 连接 id</param>
    /// <param name="hubMethod">客户端方法名</param>
    /// <param name="args">位置参数</param>
    /// <param name="cancellationToken">取消发送</param>
    public Task SendToConnectionAsync(string connectionId, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hubMethod);
        return _hubContext.Clients.Client(connectionId).SendCoreAsync(hubMethod, args ?? [], cancellationToken);
    }

    /// <summary>向指定组调用客户端方法。</summary>
    /// <param name="groupName">组名</param>
    /// <param name="hubMethod">客户端方法名</param>
    /// <param name="args">位置参数</param>
    /// <param name="cancellationToken">取消发送</param>
    public Task SendToGroupAsync(string groupName, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        ArgumentException.ThrowIfNullOrWhiteSpace(hubMethod);
        return _hubContext.Clients.Group(groupName).SendCoreAsync(hubMethod, args ?? [], cancellationToken);
    }
}