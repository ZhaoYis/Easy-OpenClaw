using System.Collections.Concurrent;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 按事件名将网关推送分发到已注册的异步处理器；支持同一事件多处理器、通配符 <c>"*"</c> 以及序号连续性检测。
/// </summary>
public sealed class EventRouter
{
    private readonly ConcurrentDictionary<string, List<Func<GatewayEvent, Task>>> _handlers = new();
    private long _lastSeq = -1;

    /// <summary>
    /// 注册一个异步事件处理器到指定事件名。
    /// 同一事件可注册多个处理器，它们在事件触发时并行执行。
    /// 使用 <c>"*"</c> 作为事件名可订阅所有事件（通配符处理器）。
    /// 线程安全：内部使用锁保护处理器列表的并发修改。
    /// </summary>
    /// <param name="eventName">要监听的事件名称，或 "*" 订阅所有事件</param>
    /// <param name="handler">接收 <see cref="GatewayEvent"/> 并返回 Task 的异步回调</param>
    public void On(string eventName, Func<GatewayEvent, Task> handler)
    {
        var list = _handlers.GetOrAdd(eventName, _ => []);
        lock (list)
        {
            list.Add(handler);
        }
    }

    /// <summary>
    /// 移除指定事件名下的所有处理器。
    /// 常用于一次性事件（如 connect_challenge）在处理完成后清理注册。
    /// </summary>
    /// <param name="eventName">要移除处理器的事件名称</param>
    public void Off(string eventName)
    {
        _handlers.TryRemove(eventName, out _);
    }

    /// <summary>
    /// 将网关事件分发到所有匹配的处理器。
    /// 分发逻辑：先检查序列号连续性，然后同时触发精确匹配和通配符 "*" 的处理器，
    /// 所有处理器并行执行并等待全部完成。单个处理器的异常不会影响其他处理器。
    /// </summary>
    /// <param name="evt">要分发的网关事件</param>
    public async Task DispatchAsync(GatewayEvent evt)
    {
        CheckSeq(evt);

        var tasks = new List<Task>();

        if (_handlers.TryGetValue(evt.Event, out var specific))
        {
            Func<GatewayEvent, Task>[] snapshot;
            lock (specific) { snapshot = [.. specific]; }
            foreach (var h in snapshot)
                tasks.Add(SafeInvoke(h, evt));
        }

        if (_handlers.TryGetValue("*", out var wildcard))
        {
            Func<GatewayEvent, Task>[] snapshot;
            lock (wildcard) { snapshot = [.. wildcard]; }
            foreach (var h in snapshot)
                tasks.Add(SafeInvoke(h, evt));
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 检查事件序列号的连续性。若检测到序列号跳跃（gap），记录警告日志。
    /// 序列号由服务端递增分配，gap 通常意味着有事件丢失（网络问题或重连导致）。
    /// 使用 <see cref="Interlocked.Exchange"/> 保证线程安全的原子更新。
    /// </summary>
    /// <param name="evt">当前事件，其 Seq 字段可能为 null（部分事件不带序列号）</param>
    private void CheckSeq(GatewayEvent evt)
    {
        if (evt.Seq is not { } seq) return;

        var prev = Interlocked.Exchange(ref _lastSeq, seq);
        if (prev >= 0 && seq != prev + 1)
        {
            Log.Warn($"Seq gap detected: expected {prev + 1}, got {seq}");
        }
    }

    /// <summary>
    /// 安全地调用单个事件处理器。捕获处理器抛出的所有异常并记录错误日志，
    /// 确保一个处理器的异常不会阻止其他处理器的执行或导致事件分发中断。
    /// </summary>
    /// <param name="handler">要调用的异步处理器</param>
    /// <param name="evt">传递给处理器的事件对象</param>
    private static async Task SafeInvoke(Func<GatewayEvent, Task> handler, GatewayEvent evt)
    {
        try
        {
            await handler(evt);
        }
        catch (Exception ex)
        {
            Log.Error($"Event handler error [{evt.Event}]: {ex.Message}");
        }
    }
}
