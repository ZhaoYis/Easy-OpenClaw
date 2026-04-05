using System.Collections.Concurrent;
using System.Text.Json;
using OpenClaw.Core.Client;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.AutoApprove;

/// <summary>
/// Polls device.pair.list at a fixed interval and automatically approves all pending pairing requests.
/// Maintains a deduplication set to avoid redundant approve calls.
/// </summary>
public sealed class AutoApproveService
{
    private readonly GatewayClient _client;
    private readonly TimeSpan _pollInterval;
    private readonly ConcurrentDictionary<string, byte> _approvedIds = new();

    public AutoApproveService(GatewayClient client, TimeSpan? pollInterval = null)
    {
        _client = client;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        Log.Info("自动审批服务已启动，轮询间隔: " + _pollInterval.TotalSeconds + "s");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollPendingAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (TimeoutException)
            {
                Log.Warn("device.pair.list 请求超时，将在下次轮询重试");
            }
            catch (Exception ex)
            {
                Log.Error($"轮询异常: {ex.Message}");
            }

            try
            {
                await Task.Delay(_pollInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Log.Info("自动审批服务已停止");
    }

    private async Task PollPendingAsync(CancellationToken ct)
    {
        var resp = await _client.SendRequestAsync(
            "device.pair.list",
            new { },
            ct);

        if (!resp.Ok)
        {
            var errorText = resp.Error?.GetRawText() ?? "unknown";

            if (errorText.Contains("NOT_PAIRED", StringComparison.OrdinalIgnoreCase))
            {
                Log.Error("当前服务自身尚未被批准配对（NOT_PAIRED）");
                Log.Error("请先在 Gateway 控制面板中手动批准此设备，然后重新启动服务");
                throw new InvalidOperationException("NOT_PAIRED: 服务自身未被批准，无法继续运行");
            }

            Log.Error($"device.pair.list 失败: {errorText}");
            return;
        }

        if (resp.Payload is not { } payload)
        {
            Log.Debug("device.pair.list 返回空 payload");
            return;
        }

        var pairList = JsonSerializer.Deserialize<PairListResponse>(payload.GetRawText(), JsonDefaults.SerializerOptions);
        if (pairList is null)
        {
            Log.Warn("device.pair.list 反序列化失败");
            return;
        }

        var pending = pairList.Pending;
        if (pending.Length == 0)
        {
            Log.Debug("无待审批的配对请求");
            return;
        }

        Log.Info($"发现 {pending.Length} 个待审批设备");

        foreach (var request in pending)
        {
            if (string.IsNullOrEmpty(request.RequestId))
                continue;

            if (!_approvedIds.TryAdd(request.RequestId, 0))
            {
                Log.Debug($"跳过已处理的请求: {request.RequestId}");
                continue;
            }

            await ApproveAsync(request, ct);
        }
    }

    private async Task ApproveAsync(PairRequest request, CancellationToken ct)
    {
        var label = !string.IsNullOrEmpty(request.DeviceId)
            ? request.DeviceId[..Math.Min(request.DeviceId.Length, 16)]
            : request.RequestId[..Math.Min(request.RequestId.Length, 16)];

        try
        {
            var resp = await _client.SendRequestAsync(
                "device.pair.approve",
                new PairApproveParams { RequestId = request.RequestId },
                ct);

            if (resp.Ok)
            {
                var detail = BuildDeviceDetail(request);
                Log.Success($"已批准设备: {label}{detail}");
            }
            else
            {
                var errText = resp.Error?.GetRawText() ?? "unknown";
                Log.Error($"approve 失败 [{label}]: {errText}");
                _approvedIds.TryRemove(request.RequestId, out _);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error($"approve 异常 [{label}]: {ex.Message}");
            _approvedIds.TryRemove(request.RequestId, out _);
        }
    }

    private static string BuildDeviceDetail(PairRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(request.ClientId)) parts.Add($"client={request.ClientId}");
        if (!string.IsNullOrEmpty(request.Platform)) parts.Add($"platform={request.Platform}");
        if (!string.IsNullOrEmpty(request.Ip)) parts.Add($"ip={request.Ip}");
        return parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "";
    }
}
