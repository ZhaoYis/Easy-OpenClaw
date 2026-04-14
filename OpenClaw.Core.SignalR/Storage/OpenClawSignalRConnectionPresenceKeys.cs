namespace OpenClaw.Core.SignalR;

/// <summary>
/// 连接运营载荷缓存键与索引 token（<c>userKeySegment|connectionId</c>）；已认证用户使用完整 userId 经
/// <see cref="OpenClawSignalRGroupNames.NormalizeSegment"/> 后的段，匿名连接使用配置的匿名段。
/// </summary>
public static class OpenClawSignalRConnectionPresenceKeys
{
    /// <summary>用于载荷键与索引的 user 段。</summary>
    public static string GetUserKeySegment(string? resolvedUserId, OpenClawSignalROptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.IsNullOrWhiteSpace(resolvedUserId))
            return OpenClawSignalRGroupNames.NormalizeSegment(resolvedUserId);

        var anon = options.ConnectionPresenceAnonymousKeySegment;
        if (string.IsNullOrWhiteSpace(anon))
            anon = "anon";
        return OpenClawSignalRGroupNames.NormalizeSegment(anon);
    }

    public static string FormatPayloadKey(string prefix, string userKeySegment, string connectionId)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(userKeySegment);
        ArgumentNullException.ThrowIfNull(connectionId);
        return prefix + userKeySegment + ":" + connectionId;
    }

    /// <summary>分布式/内存索引中存储的复合 token（user 段不含 <c>|</c>）。</summary>
    public static string FormatIndexToken(string userKeySegment, string connectionId)
    {
        ArgumentNullException.ThrowIfNull(userKeySegment);
        ArgumentNullException.ThrowIfNull(connectionId);
        return userKeySegment + "|" + connectionId;
    }

    public static bool TryParseIndexToken(string token, out string userKeySegment, out string connectionId)
    {
        userKeySegment = "";
        connectionId = "";
        var i = token.IndexOf('|');
        if (i <= 0 || i >= token.Length - 1)
            return false;

        userKeySegment = token[..i];
        connectionId = token[(i + 1)..];
        return connectionId.Length > 0;
    }

    public static string PayloadKeyForSnapshot(OpenClawSignalRConnectionSnapshot snapshot, OpenClawSignalROptions options) =>
        FormatPayloadKey(
            options.ConnectionPresencePayloadKeyPrefix,
            GetUserKeySegment(snapshot.UserId, options),
            snapshot.ConnectionId);

    public static string IndexTokenForSnapshot(OpenClawSignalRConnectionSnapshot snapshot, OpenClawSignalROptions options) =>
        FormatIndexToken(GetUserKeySegment(snapshot.UserId, options), snapshot.ConnectionId);

    public static string PayloadKeyForRemoval(string connectionId, string? presenceUserId, OpenClawSignalROptions options) =>
        FormatPayloadKey(
            options.ConnectionPresencePayloadKeyPrefix,
            GetUserKeySegment(presenceUserId, options),
            connectionId);

    public static string IndexTokenForRemoval(string connectionId, string? presenceUserId, OpenClawSignalROptions options) =>
        FormatIndexToken(GetUserKeySegment(presenceUserId, options), connectionId);
}
