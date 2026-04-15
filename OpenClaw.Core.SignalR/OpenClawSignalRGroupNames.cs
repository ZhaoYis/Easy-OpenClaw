using System.Text;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// SignalR 组名片段规范化，避免非法字符导致协议问题。
/// </summary>
public static class OpenClawSignalRGroupNames
{
    private const int MaxSegmentLength = 128;

    /// <summary>
    /// 将用户 id、档位等片段规范为组名后缀：仅保留 ASCII 字母数字与 <c>-_.:</c>，其余替换为下划线，并截断至 <c>128</c> 字符。
    /// </summary>
    /// <param name="value">原始片段，不可为 null</param>
    /// <returns>非空规范化字符串（全非法时返回 <c>_</c>）</returns>
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

    /// <summary>与 Hub 加入的单用户组名一致：<see cref="OpenClawSignalROptions.UserGroupPrefix"/> + <see cref="NormalizeSegment"/>（用户 id）。</summary>
    public static string FormatUserGroup(OpenClawSignalROptions options, string userId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(userId);
        return options.UserGroupPrefix + NormalizeSegment(userId);
    }

    /// <summary>与 Hub 加入的档位组名一致：<see cref="OpenClawSignalROptions.TierGroupPrefix"/> + <see cref="NormalizeSegment"/>（档位）。</summary>
    public static string FormatTierGroup(OpenClawSignalROptions options, string tier)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tier);
        return options.TierGroupPrefix + NormalizeSegment(tier);
    }
}
