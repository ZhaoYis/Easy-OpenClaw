using Microsoft.Extensions.Options;
using OpenClaw.Core.Client;
using OpenClaw.Core.Models;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="GatewayRequestManager"/> 的单元测试：注册、完成、超时与批量取消。
/// </summary>
public sealed class GatewayRequestManagerTests
{
    /// <summary>
    /// <see cref="GatewayRequestManager.TryComplete"/> 在 id 存在时应完成 Task 并返回 true。
    /// </summary>
    [Fact]
    public async Task TryComplete_when_pending_returns_true_and_sets_result()
    {
        var opts = Options.Create(new GatewayOptions { RequestTimeout = TimeSpan.FromSeconds(30) });
        var mgr = new GatewayRequestManager(opts);
        var (id, task) = mgr.Register();
        var ok = mgr.TryComplete(id, new GatewayResponse { Id = id, Ok = true });
        Assert.True(ok);
        var res = await task;
        Assert.True(res.Ok);
    }

    /// <summary>
    /// 对未知 id 完成时应返回 false。
    /// </summary>
    [Fact]
    public void TryComplete_unknown_id_returns_false()
    {
        var opts = Options.Create(new GatewayOptions());
        var mgr = new GatewayRequestManager(opts);
        Assert.False(mgr.TryComplete("nope", new GatewayResponse { Id = "nope", Ok = true }));
    }

    /// <summary>
    /// 超时后等待 Task 应抛出 <see cref="TimeoutException"/>。
    /// </summary>
    [Fact]
    public async Task Register_times_out_when_not_completed()
    {
        var opts = Options.Create(new GatewayOptions { RequestTimeout = TimeSpan.FromMilliseconds(80) });
        var mgr = new GatewayRequestManager(opts);
        var (_, task) = mgr.Register(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAsync<TimeoutException>(() => task);
    }

    /// <summary>
    /// <see cref="GatewayRequestManager.CancelAll"/> 应将进行中请求标记为取消。
    /// </summary>
    [Fact]
    public async Task CancelAll_marks_tasks_cancelled()
    {
        var opts = Options.Create(new GatewayOptions { RequestTimeout = TimeSpan.FromSeconds(10) });
        var mgr = new GatewayRequestManager(opts);
        var (_, task) = mgr.Register();
        mgr.CancelAll();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }
}
