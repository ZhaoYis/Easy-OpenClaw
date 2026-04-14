using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <inheritdoc />
public sealed class OpenClawSignalROperationService<THub> : IOpenClawSignalROperationService<THub>
    where THub : Hub
{
    private readonly IHubContext<THub> _hubContext;
    private readonly IOpenClawSignalRConnectionPresenceStore _presenceStore;
    private readonly IOptions<OpenClawSignalROptions> _options;

    public OpenClawSignalROperationService(
        IHubContext<THub> hubContext,
        IOpenClawSignalRConnectionPresenceStore presenceStore,
        IOptions<OpenClawSignalROptions> options)
    {
        _hubContext = hubContext;
        _presenceStore = presenceStore;
        _options = options;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetOnlineConnectionsAsync(
        CancellationToken cancellationToken = default) =>
        _presenceStore.GetSnapshotsAsync(cancellationToken).AsTask();

    /// <inheritdoc />
    public async Task<int> GetOnlineConnectionCountAsync(CancellationToken cancellationToken = default)
    {
        var list = await GetOnlineConnectionsAsync(cancellationToken).ConfigureAwait(false);
        return list.Count;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> GetConnectionsForUserAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var snapshots = await _presenceStore.GetSnapshotsAsync(cancellationToken).ConfigureAwait(false);
        return snapshots
            .Where(s => string.Equals(s.UserId, userId, StringComparison.Ordinal))
            .ToList();
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public string FormatUserGroupName(string userId) =>
        OpenClawSignalRGroupNames.FormatUserGroup(_options.Value, userId);

    /// <inheritdoc />
    public string FormatTierGroupName(string tier) =>
        OpenClawSignalRGroupNames.FormatTierGroup(_options.Value, tier);

    /// <inheritdoc />
    public Task SendToUserAsync(string userId, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hubMethod);
        return _hubContext.Clients.User(userId).SendCoreAsync(hubMethod, args ?? [], cancellationToken);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task SendToConnectionAsync(string connectionId, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hubMethod);
        return _hubContext.Clients.Client(connectionId).SendCoreAsync(hubMethod, args ?? [], cancellationToken);
    }

    /// <inheritdoc />
    public Task SendToGroupAsync(string groupName, string hubMethod, object?[]? args = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        ArgumentException.ThrowIfNullOrWhiteSpace(hubMethod);
        return _hubContext.Clients.Group(groupName).SendCoreAsync(hubMethod, args ?? [], cancellationToken);
    }
}