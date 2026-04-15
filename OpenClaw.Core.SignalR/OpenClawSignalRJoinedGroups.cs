using System.Security.Claims;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 与 <see cref="OpenClawGatewayHubBase"/> 中 <c>OnConnectedAsync</c> 加入的组集合保持一致。
/// 宿主在 Hub 外（例如受众解析、运维脚本）需要相同组名时应调用 <see cref="Build"/>，勿手写拼接规则。
/// </summary>
public static class OpenClawSignalRJoinedGroups
{
    /// <summary>
    /// 计算与 Hub <c>OnConnectedAsync</c> 一致的组列表：<paramref name="additionalGroups"/> 优先，
    /// 已认证时再追加用户组、（可选）档位组与系统广播组。
    /// </summary>
    /// <param name="user">当前连接的 Claims，匿名时可为 null</param>
    /// <param name="isAuthenticated">是否与 Hub 中判定一致</param>
    /// <param name="opts">分组前缀与 Claim 类型配置</param>
    /// <param name="additionalGroups">基类 <see cref="OpenClawGatewayHubBase.GetAdditionalConnectionGroups"/> 返回的额外组</param>
    /// <returns>去空白后的组名列表（顺序：额外组 → 用户组 → 档位组 → 系统广播）</returns>
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

    /// <summary>
    /// 为运营快照解析档位 Claim；未认证或未配置 <see cref="OpenClawSignalROptions.TierClaimType"/> 时返回 null。
    /// </summary>
    public static string? ResolveTierForSnapshot(ClaimsPrincipal? user, OpenClawSignalROptions opts)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;
        if (string.IsNullOrWhiteSpace(opts.TierClaimType))
            return null;
        return OpenClawSignalRClaimResolution.GetTier(user, opts.TierClaimType);
    }
}
