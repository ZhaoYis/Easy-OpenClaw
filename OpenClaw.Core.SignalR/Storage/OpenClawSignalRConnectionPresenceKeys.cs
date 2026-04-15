namespace OpenClaw.Core.SignalR;

/// <summary>
/// 连接运营载荷缓存键与索引 token（<c>userKeySegment|connectionId</c>）；已认证用户使用完整 userId 经
/// <see cref="OpenClawSignalRGroupNames.NormalizeSegment"/> 后的段，匿名连接使用配置的匿名段。
/// </summary>
public static class OpenClawSignalRConnectionPresenceKeys
{
    /// <summary>
    /// 计算载荷键与分布式索引共用的 user 段：有用户 id 时为其规范化形式，否则使用匿名段（配置或默认 <c>anon</c>）。
    /// </summary>
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

    /// <summary>拼接 <c>prefix + userKeySegment + ":" + connectionId</c>，用作缓存/Hybrid 载荷键。</summary>
    public static string FormatPayloadKey(string prefix, string userKeySegment, string connectionId)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(userKeySegment);
        ArgumentNullException.ThrowIfNull(connectionId);
        return prefix + userKeySegment + ":" + connectionId;
    }

    /// <summary>生成分布式/内存索引中的复合 token：<c>userKeySegment|connectionId</c>（user 段不得含 <c>|</c>）。</summary>
    public static string FormatIndexToken(string userKeySegment, string connectionId)
    {
        ArgumentNullException.ThrowIfNull(userKeySegment);
        ArgumentNullException.ThrowIfNull(connectionId);
        return userKeySegment + "|" + connectionId;
    }

    /// <summary>将索引 token 拆回 user 段与连接 id；格式非法时返回 false。</summary>
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

    /// <summary>根据快照中的 <see cref="OpenClawSignalRConnectionSnapshot.UserId"/> 生成载荷键。</summary>
    public static string PayloadKeyForSnapshot(OpenClawSignalRConnectionSnapshot snapshot, OpenClawSignalROptions options) =>
        FormatPayloadKey(
            options.ConnectionPresencePayloadKeyPrefix,
            GetUserKeySegment(snapshot.UserId, options),
            snapshot.ConnectionId);

    /// <summary>根据快照生成索引 token。</summary>
    public static string IndexTokenForSnapshot(OpenClawSignalRConnectionSnapshot snapshot, OpenClawSignalROptions options) =>
        FormatIndexToken(GetUserKeySegment(snapshot.UserId, options), snapshot.ConnectionId);

    /// <summary>断开时按连接 id 与注册时相同的 user 段计算载荷键（与 <see cref="IOpenClawSignalRConnectionPresenceStore.RegisterAsync"/> 写入键一致）。</summary>
    public static string PayloadKeyForRemoval(string connectionId, string? presenceUserId, OpenClawSignalROptions options) =>
        FormatPayloadKey(
            options.ConnectionPresencePayloadKeyPrefix,
            GetUserKeySegment(presenceUserId, options),
            connectionId);

    /// <summary>断开时计算与注册时一致的索引 token。</summary>
    public static string IndexTokenForRemoval(string connectionId, string? presenceUserId, OpenClawSignalROptions options) =>
        FormatIndexToken(GetUserKeySegment(presenceUserId, options), connectionId);
}
