using System.Threading;
using OpenClaw.Core.Client;
using OpenClaw.Core.Models;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="EventRouter"/> 的单元测试：注册、移除、并行分发、序列号缺口与处理器异常隔离。
/// </summary>
public sealed class EventRouterTests
{
    /// <summary>
    /// 验证精确事件名与通配符处理器在分发时均被调用。
    /// </summary>
    [Fact]
    public async Task DispatchAsync_invokes_specific_and_wildcard_handlers()
    {
        var router = new EventRouter();
        var specificHits = 0;
        var wildHits = 0;
        router.On("evt", _ => { Interlocked.Increment(ref specificHits); return Task.CompletedTask; });
        router.On("*", _ => { Interlocked.Increment(ref wildHits); return Task.CompletedTask; });

        await router.DispatchAsync(new GatewayEvent { Event = "evt" });

        Assert.Equal(1, specificHits);
        Assert.Equal(1, wildHits);
    }

    /// <summary>
    /// 验证 <see cref="EventRouter.Off"/> 移除后不再触发该事件下的处理器。
    /// </summary>
    [Fact]
    public async Task Off_removes_handlers_for_event_name()
    {
        var router = new EventRouter();
        var hits = 0;
        router.On("x", _ => { Interlocked.Increment(ref hits); return Task.CompletedTask; });
        router.Off("x");
        await router.DispatchAsync(new GatewayEvent { Event = "x" });
        Assert.Equal(0, hits);
    }

    /// <summary>
    /// 当事件携带递增 seq 出现跳跃时应记录警告但不影响分发（由 Log 输出，此处仅确保不抛异常）。
    /// </summary>
    [Fact]
    public async Task DispatchAsync_seq_gap_does_not_throw()
    {
        var router = new EventRouter();
        await router.DispatchAsync(new GatewayEvent { Event = "a", Seq = 1 });
        await router.DispatchAsync(new GatewayEvent { Event = "a", Seq = 5 });
    }

    /// <summary>
    /// 单个处理器抛错时，其它处理器仍应执行完毕（SafeInvoke 吞掉异常）。
    /// </summary>
    [Fact]
    public async Task DispatchAsync_one_handler_failure_does_not_block_others()
    {
        var router = new EventRouter();
        var secondRan = false;
        router.On("e", _ => throw new InvalidOperationException("boom"));
        router.On("e", _ => { secondRan = true; return Task.CompletedTask; });
        await router.DispatchAsync(new GatewayEvent { Event = "e" });
        Assert.True(secondRan);
    }

    /// <summary>
    /// 订阅时传入的 state 应在分发时原样交给 handler。
    /// </summary>
    [Fact]
    public async Task DispatchAsync_passes_subscription_state_to_handler()
    {
        var router = new EventRouter();
        var key = new object();
        object? received = null;
        router.On("evt", key, (_, s) =>
        {
            received = s;
            return Task.CompletedTask;
        });

        await router.DispatchAsync(new GatewayEvent { Event = "evt" });

        Assert.Same(key, received);
    }

    /// <summary>
    /// <see cref="EventRouter.Off(string, object?)"/> 只移除与 state 匹配的订阅，其它 state 保留。
    /// </summary>
    [Fact]
    public async Task Off_with_state_removes_only_matching_handlers()
    {
        var router = new EventRouter();
        var keyA = new object();
        var keyB = new object();
        var hitsA = 0;
        var hitsB = 0;
        router.On("e", keyA, (_, _) => { Interlocked.Increment(ref hitsA); return Task.CompletedTask; });
        router.On("e", keyB, (_, _) => { Interlocked.Increment(ref hitsB); return Task.CompletedTask; });

        router.Off("e", keyA);
        await router.DispatchAsync(new GatewayEvent { Event = "e" });

        Assert.Equal(0, hitsA);
        Assert.Equal(1, hitsB);
    }

    /// <summary>
    /// <c>Off(name, null)</c> 仅移除订阅时 state 为 <c>null</c> 的处理器。
    /// </summary>
    [Fact]
    public async Task Off_with_null_state_removes_only_null_state_handlers()
    {
        var router = new EventRouter();
        var key = new object();
        var nullHits = 0;
        var keyHits = 0;
        router.On("e", null, (_, _) => { Interlocked.Increment(ref nullHits); return Task.CompletedTask; });
        router.On("e", key, (_, _) => { Interlocked.Increment(ref keyHits); return Task.CompletedTask; });

        router.Off("e", null);
        await router.DispatchAsync(new GatewayEvent { Event = "e" });

        Assert.Equal(0, nullHits);
        Assert.Equal(1, keyHits);
    }
}
