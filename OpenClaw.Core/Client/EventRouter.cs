using System.Collections.Concurrent;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// Dispatches gateway events to registered async handlers by event name.
/// Supports multiple handlers per event, wildcard "*" handler, and seq tracking.
/// </summary>
public sealed class EventRouter
{
    private readonly ConcurrentDictionary<string, List<Func<GatewayEvent, Task>>> _handlers = new();
    private long _lastSeq = -1;

    public void On(string eventName, Func<GatewayEvent, Task> handler)
    {
        var list = _handlers.GetOrAdd(eventName, _ => []);
        lock (list)
        {
            list.Add(handler);
        }
    }

    public void Off(string eventName)
    {
        _handlers.TryRemove(eventName, out _);
    }

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

    private void CheckSeq(GatewayEvent evt)
    {
        if (evt.Seq is not { } seq) return;

        var prev = Interlocked.Exchange(ref _lastSeq, seq);
        if (prev >= 0 && seq != prev + 1)
        {
            Log.Warn($"Seq gap detected: expected {prev + 1}, got {seq}");
        }
    }

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
