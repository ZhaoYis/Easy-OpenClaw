using System.Security.Claims;
using OpenClaw.Core.SignalR;
using Xunit;

namespace OpenClaw.Tests.SignalR;

public sealed class OpenClawSignalRClaimResolutionAndJoinedGroupsTests
{
    [Fact]
    public void GetUserId_prefers_configured_claim_type()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "from-sub"),
                new Claim(ClaimTypes.NameIdentifier, "from-nameid"),
                new Claim("custom", "from-custom"),
            ],
            authenticationType: "Test",
            nameType: null,
            roleType: null));

        Assert.Equal("from-custom", OpenClawSignalRClaimResolution.GetUserId(user, "custom"));
    }

    [Fact]
    public void GetUserId_falls_back_to_name_identifier_then_sub()
    {
        var user1 = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "nid")],
            "Test"));
        Assert.Equal("nid", OpenClawSignalRClaimResolution.GetUserId(user1, "missing"));

        var user2 = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "subval")],
            "Test"));
        Assert.Equal("subval", OpenClawSignalRClaimResolution.GetUserId(user2, "missing"));
    }

    [Fact]
    public void GetTier_uses_role_when_tier_claim_type_empty()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "admin")],
            "Test"));
        Assert.Equal("admin", OpenClawSignalRClaimResolution.GetTier(user, ""));
    }

    [Fact]
    public void GetTier_uses_configured_claim_type_when_set()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "ignored"),
                new Claim("tier", "paid"),
            ],
            "Test"));
        Assert.Equal("paid", OpenClawSignalRClaimResolution.GetTier(user, "tier"));
    }

    [Fact]
    public void Build_anonymous_returns_only_additional_groups()
    {
        var opts = new OpenClawSignalROptions();
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var groups = OpenClawSignalRJoinedGroups.Build(user, false, opts, ["g1", "", "g2"]);
        Assert.Equal(["g1", "g2"], groups);
    }

    [Fact]
    public void Build_authenticated_adds_user_tier_system_groups()
    {
        var opts = new OpenClawSignalROptions
        {
            UserIdClaimType = "sub",
            TierClaimType = "tier",
            UserGroupPrefix = "oc:user:",
            TierGroupPrefix = "oc:tier:",
            SystemBroadcastGroupName = "oc:system",
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "u1"),
                new Claim("tier", "paid"),
            ],
            "Test",
            "sub",
            ClaimTypes.Role));

        var groups = OpenClawSignalRJoinedGroups.Build(user, true, opts, ["extra"]);
        Assert.Contains("extra", groups);
        Assert.Contains("oc:user:u1", groups);
        Assert.Contains("oc:tier:paid", groups);
        Assert.Contains("oc:system", groups);
    }

    [Fact]
    public void ResolveTierForSnapshot_null_when_not_authenticated_or_no_tier_type()
    {
        var opts = new OpenClawSignalROptions { TierClaimType = "tier" };
        var anon = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.Null(OpenClawSignalRJoinedGroups.ResolveTierForSnapshot(anon, opts));

        var optsNoTier = new OpenClawSignalROptions { TierClaimType = null };
        var auth = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tier", "x")], "Test"));
        Assert.Null(OpenClawSignalRJoinedGroups.ResolveTierForSnapshot(auth, optsNoTier));
    }

    [Fact]
    public void ResolveTierForSnapshot_returns_claim_when_authenticated()
    {
        var opts = new OpenClawSignalROptions { TierClaimType = "tier" };
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tier", "gold")], "Test"));
        Assert.Equal("gold", OpenClawSignalRJoinedGroups.ResolveTierForSnapshot(principal, opts));
    }
}
