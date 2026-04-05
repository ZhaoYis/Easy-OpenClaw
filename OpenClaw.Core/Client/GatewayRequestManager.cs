using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// Tracks in-flight requests and correlates them with responses by id.
/// Singleton 生命周期：持有所有进行中请求的状态，贯穿整个应用生命周期。
/// </summary>
public sealed class GatewayRequestManager
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<GatewayResponse>> _pending = new();
    private readonly TimeSpan _defaultTimeout;

    /// <summary>
    /// 初始化请求管理器，从配置中读取默认的请求超时时间。
    /// </summary>
    /// <param name="options">网关配置，提供 <see cref="GatewayOptions.RequestTimeout"/> 默认超时值</param>
    public GatewayRequestManager(IOptions<GatewayOptions> options)
    {
        _defaultTimeout = options.Value.RequestTimeout;
    }

    /// <summary>
    /// 生成下一个唯一请求 ID（基于 GUID）。用于标识每个发送到网关的 RPC 请求，
    /// 以便在收到响应时将其与原始请求进行关联。
    /// </summary>
    /// <returns>新的唯一请求 ID 字符串</returns>
    private string NextId() => Guid.NewGuid().ToString();

    /// <summary>
    /// 注册一个新的待处理请求。生成唯一 ID，创建 <see cref="TaskCompletionSource{T}"/> 用于异步等待响应，
    /// 并启动超时计时器——若在指定时间内未收到响应，自动将 Task 标记为 <see cref="TimeoutException"/>
    /// 并从待处理字典中移除。
    /// </summary>
    /// <param name="timeout">自定义超时时间，为 null 时使用配置中的默认超时</param>
    /// <returns>
    /// 元组包含：
    /// - id: 请求的唯一标识符，需写入发送的 JSON 帧
    /// - task: 异步等待响应的 Task，完成时返回 <see cref="GatewayResponse"/>
    /// </returns>
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

    /// <summary>
    /// 尝试完成指定 ID 的待处理请求。从字典中移除请求并将响应设置到 TaskCompletionSource，
    /// 使等待该请求的调用方收到响应结果。
    /// 若请求已超时或不存在于字典中，返回 false。
    /// </summary>
    /// <param name="id">请求 ID（与发送帧中的 id 字段对应）</param>
    /// <param name="response">从网关收到的响应对象</param>
    /// <returns>是否成功找到并完成了对应的请求</returns>
    public bool TryComplete(string id, GatewayResponse response)
    {
        if (_pending.TryRemove(id, out var tcs))
        {
            tcs.TrySetResult(response);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 取消所有进行中的请求。遍历并移除所有待处理的 TaskCompletionSource，
    /// 将每个 Task 标记为已取消状态。通常在客户端断开连接或释放时调用。
    /// </summary>
    public void CancelAll()
    {
        foreach (var kvp in _pending)
        {
            if (_pending.TryRemove(kvp.Key, out var tcs))
                tcs.TrySetCanceled();
        }
    }
}