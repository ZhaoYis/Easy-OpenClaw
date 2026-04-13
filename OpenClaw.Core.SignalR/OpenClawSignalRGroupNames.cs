using System.Text;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// SignalR 组名片段规范化，避免非法字符导致协议问题。
/// </summary>
public static class OpenClawSignalRGroupNames
{
    private const int MaxSegmentLength = 128;

    /// <summary>
    /// 将用户 id、档位等片段规范为组名后缀：仅保留字母数字与 <c>-_.:</c>，其余替换为下划线，并截断长度。
    /// </summary>
    public static string NormalizeSegment(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
            return "_";

        var sb = new StringBuilder(Math.Min(value.Length, MaxSegmentLength));
        foreach (var c in value)
        {
            if (sb.Length >= MaxSegmentLength)
                break;
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':')
                sb.Append(c);
            else
                sb.Append('_');
        }

        return sb.Length == 0 ? "_" : sb.ToString();
    }

    /// <summary>与 Hub 加入的单用户组名一致：<c>UserGroupPrefix</c> + 规范化后的用户 id。</summary>
    public static string FormatUserGroup(OpenClawSignalROptions options, string userId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(userId);
        return options.UserGroupPrefix + NormalizeSegment(userId);
    }

    /// <summary>与 Hub 加入的档位组名一致：<c>TierGroupPrefix</c> + 规范化后的档位值。</summary>
    public static string FormatTierGroup(OpenClawSignalROptions options, string tier)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tier);
        return options.TierGroupPrefix + NormalizeSegment(tier);
    }
}
