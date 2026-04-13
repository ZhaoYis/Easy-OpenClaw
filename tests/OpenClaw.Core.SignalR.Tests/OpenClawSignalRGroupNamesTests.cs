using OpenClaw.Core.SignalR;
using Xunit;

namespace OpenClaw.Tests.SignalR;

public sealed class OpenClawSignalRGroupNamesTests
{
    [Theory]
    [InlineData("abc123", "abc123")]
    [InlineData("a-b_c.d:Z", "a-b_c.d:Z")]
    [InlineData("user@host", "user_host")]
    public void NormalizeSegment_keeps_allowed_chars_and_replaces_others(string input, string expected) =>
        Assert.Equal(expected, OpenClawSignalRGroupNames.NormalizeSegment(input));

    [Fact]
    public void NormalizeSegment_empty_returns_underscore() =>
        Assert.Equal("_", OpenClawSignalRGroupNames.NormalizeSegment(""));

    [Fact]
    public void NormalizeSegment_invalid_chars_each_become_underscore() =>
        Assert.Equal("___", OpenClawSignalRGroupNames.NormalizeSegment("@@@"));

    [Fact]
    public void NormalizeSegment_truncates_to_max_length()
    {
        var s = new string('a', 200);
        var n = OpenClawSignalRGroupNames.NormalizeSegment(s);
        Assert.Equal(128, n.Length);
        Assert.Equal(new string('a', 128), n);
    }

    [Fact]
    public void NormalizeSegment_null_throws() =>
        Assert.Throws<ArgumentNullException>(() => OpenClawSignalRGroupNames.NormalizeSegment(null!));

    [Fact]
    public void FormatUserGroup_composes_prefix_and_normalized_id()
    {
        var opts = new OpenClawSignalROptions { UserGroupPrefix = "oc:user:" };
        Assert.Equal("oc:user:u1", OpenClawSignalRGroupNames.FormatUserGroup(opts, "u1"));
    }

    [Fact]
    public void FormatTierGroup_composes_prefix_and_normalized_tier()
    {
        var opts = new OpenClawSignalROptions { TierGroupPrefix = "oc:tier:" };
        Assert.Equal("oc:tier:paid", OpenClawSignalRGroupNames.FormatTierGroup(opts, "paid"));
    }

    [Fact]
    public void FormatUserGroup_null_options_throws() =>
        Assert.Throws<ArgumentNullException>(() => OpenClawSignalRGroupNames.FormatUserGroup(null!, "x"));

    [Fact]
    public void FormatUserGroup_null_userId_throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            OpenClawSignalRGroupNames.FormatUserGroup(new OpenClawSignalROptions(), null!));
}
