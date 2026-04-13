using System.Security.Claims;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 与 <see cref="OpenClawGatewayHubBase"/> 中 <c>OnConnectedAsync</c> 加入的组集合保持一致。
/// 宿主在 Hub 外（例如受众解析、运维脚本）需要相同组名时应调用 <see cref="Build"/>，勿手写拼接规则。
/// </summary>
public static class OpenClawSignalRJoinedGroups
{
    public static List<string> Build(
        ClaimsPrincipal? user,
        bool isAuthenticated,
        OpenClawSignalROptions opts,
        IEnumerable<string> additionalGroups)
    {
        var list = new List<string>();
        foreach (var g in additionalGroups)
        {
            if (!string.IsNullOrWhiteSpace(g))
                list.Add(g);
        }

        if (!isAuthenticated)
            return list;

        var userId = OpenClawSignalRClaimResolution.GetUserId(user, opts.UserIdClaimType);
        if (!string.IsNullOrWhiteSpace(userId))
            list.Add(OpenClawSignalRGroupNames.FormatUserGroup(opts, userId));

        if (!string.IsNullOrWhiteSpace(opts.TierClaimType))
        {
            var tier = OpenClawSignalRClaimResolution.GetTier(user, opts.TierClaimType);
            if (!string.IsNullOrWhiteSpace(tier))
                list.Add(OpenClawSignalRGroupNames.FormatTierGroup(opts, tier));
        }

        list.Add(opts.SystemBroadcastGroupName);
        return list;
    }

    public static string? ResolveTierForSnapshot(ClaimsPrincipal? user, OpenClawSignalROptions opts)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;
        if (string.IsNullOrWhiteSpace(opts.TierClaimType))
            return null;
        return OpenClawSignalRClaimResolution.GetTier(user, opts.TierClaimType);
    }
}
