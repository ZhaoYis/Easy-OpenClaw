using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// Tracks in-flight requests and correlates them with responses by id.
/// Singleton 生命周期：持有所有进行中请求的状态，贯穿整个应用生命周期。
/// </summary>
public sealed class RequestManager
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<GatewayResponse>> _pending = new();
    private readonly TimeSpan _defaultTimeout;

    public RequestManager(IOptions<GatewayOptions> options)
    {
        _defaultTimeout = options.Value.RequestTimeout;
    }

    public string NextId() => Guid.NewGuid().ToString();

    public (string id, Task<GatewayResponse> task) Register(TimeSpan? timeout = null)
    {
        var id = NextId();
        var tcs = new TaskCompletionSource<GatewayResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var effectiveTimeout = timeout ?? _defaultTimeout;
        _ = Task.Delay(effectiveTimeout).ContinueWith(_ =>
        {
            if (_pending.TryRemove(id, out var t))
                t.TrySetException(new TimeoutException($"Request {id} timed out after {effectiveTimeout.TotalSeconds}s"));
        }, TaskScheduler.Default);

        return (id, tcs.Task);
    }

    public bool TryComplete(string id, GatewayResponse response)
    {
        if (_pending.TryRemove(id, out var tcs))
        {
            tcs.TrySetResult(response);
            return true;
        }
        return false;
    }

    public void CancelAll()
    {
        foreach (var kvp in _pending)
        {
            if (_pending.TryRemove(kvp.Key, out var tcs))
                tcs.TrySetCanceled();
        }
    }
}
