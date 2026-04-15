using System.Security.Claims;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 从 <see cref="ClaimsPrincipal"/> 解析用户标识与档位；规则与
/// <see cref="OpenClawSignalRUserIdProvider"/>、<see cref="OpenClawGatewayHubBase"/> 一致。自定义
/// <see cref="IGatewayEventAudienceResolver"/>（经 <see cref="GatewayEventAudienceResolveContext"/> 与 Hub 建连逻辑）或运营逻辑应复用此类，避免与 Hub 行为分叉。
/// </summary>
public static class OpenClawSignalRClaimResolution
{
    /// <summary>
    /// 解析用户 id：优先配置的类型，其次 <see cref="ClaimTypes.NameIdentifier"/>（JWT 默认入站映射），再尝试字面 <c>sub</c>。
    /// </summary>
    /// <param name="user">当前用户；null 时返回 null</param>
    /// <param name="configuredClaimType"><see cref="OpenClawSignalROptions.UserIdClaimType"/>，如 <c>sub</c></param>
    /// <returns>非空白用户标识，无法解析时返回 null</returns>
    public static string? GetUserId(ClaimsPrincipal? user, string configuredClaimType)
    {
        if (user is null)
            return null;

        var id = user.FindFirst(configuredClaimType)?.Value;
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        return user.FindFirst("sub")?.Value;
    }

    /// <summary>
    /// 解析用户等级：优先配置的类型，其次 <see cref="ClaimTypes.Role"/>（JWT 默认入站映射）。
    /// </summary>
    /// <param name="user">当前用户；null 时返回 null</param>
    /// <param name="tierClaimType"><see cref="OpenClawSignalROptions.TierClaimType"/>；空白时使用 <see cref="ClaimTypes.Role"/></param>
    /// <returns>档位或角色字符串，无法解析时返回 null</returns>
    public static string? GetTier(ClaimsPrincipal? user, string tierClaimType)
    {
        if (user is null)
            return null;

        if (string.IsNullOrWhiteSpace(tierClaimType))
            return user.FindFirst(ClaimTypes.Role)?.Value;

        return user.FindFirst(tierClaimType)?.Value;
    }
}