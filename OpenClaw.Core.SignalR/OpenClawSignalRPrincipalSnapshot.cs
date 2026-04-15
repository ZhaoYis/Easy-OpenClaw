using System.Security.Claims;
using System.Text.Json.Serialization;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 建连时 <see cref="Microsoft.AspNetCore.SignalR.HubCallerContext.User"/> 的可序列化快照，供运营存储与后续逻辑重建身份上下文。
/// </summary>
/// <param name="IsAuthenticated">身份是否标记为已认证</param>
/// <param name="AuthenticationType">认证方案名称</param>
/// <param name="NameClaimType">名称 Claim 类型</param>
/// <param name="RoleClaimType">角色 Claim 类型</param>
/// <param name="Claims">序列化后的 Claim 列表</param>
public sealed record OpenClawSignalRPrincipalSnapshot(
    [property: JsonPropertyName("isAuthenticated")]
    bool IsAuthenticated,
    [property: JsonPropertyName("authenticationType")]
    string? AuthenticationType,
    [property: JsonPropertyName("nameClaimType")]
    string? NameClaimType,
    [property: JsonPropertyName("roleClaimType")]
    string? RoleClaimType,
    [property: JsonPropertyName("claims")] OpenClawSignalRClaimSnapshot[] Claims)
{
    /// <summary>从当前 <see cref="ClaimsPrincipal"/> 捕获快照。</summary>
    public static OpenClawSignalRPrincipalSnapshot From(ClaimsPrincipal? principal)
    {
        if (principal?.Identity is not ClaimsIdentity id)
            return new OpenClawSignalRPrincipalSnapshot(false, null, null, null, []);

        try
        {
            var snapshotClaims = id.Claims
                .Select(static c =>
                {
                    Dictionary<string, string>? props = null;
                    if (c.Properties.Count > 0)
                    {
                        props = new Dictionary<string, string>(c.Properties);
                    }

                    return new OpenClawSignalRClaimSnapshot(
                        c.Type,
                        c.Value,
                        c.ValueType,
                        c.Issuer,
                        c.OriginalIssuer,
                        props);
                })
                .ToArray();

            return new OpenClawSignalRPrincipalSnapshot(
                id.IsAuthenticated,
                id.AuthenticationType,
                id.NameClaimType,
                id.RoleClaimType,
                snapshotClaims);
        }
        catch (Exception)
        {
            return new OpenClawSignalRPrincipalSnapshot(
                id.IsAuthenticated,
                id.AuthenticationType,
                id.NameClaimType,
                id.RoleClaimType,
                []);
        }
    }

    /// <summary>重建 <see cref="ClaimsPrincipal"/>（仅服务端使用）。</summary>
    public ClaimsPrincipal ToClaimsPrincipal()
    {
        var claimObjects = Claims.Select(static c =>
            new Claim(
                c.Type,
                c.Value,
                c.ValueType ?? ClaimValueTypes.String,
                c.Issuer ?? ClaimsIdentity.DefaultIssuer,
                c.OriginalIssuer ?? ClaimsIdentity.DefaultIssuer));
        var identity = new ClaimsIdentity(
            claimObjects,
            AuthenticationType,
            NameClaimType ?? ClaimTypes.Name,
            RoleClaimType ?? ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}