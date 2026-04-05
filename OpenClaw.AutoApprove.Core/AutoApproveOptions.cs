namespace OpenClaw.AutoApprove.Core;

/// <summary>
/// 自动审批服务的配置选项。
/// </summary>
public sealed class AutoApproveOptions
{
    /// <summary>配置节名称</summary>
    public const string SectionName = "AutoApprove";

    /// <summary>轮询 device.pair.list 的间隔秒数</summary>
    public double PollIntervalSeconds { get; set; } = 2;
}
