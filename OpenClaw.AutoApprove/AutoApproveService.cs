using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Client;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.AutoApprove;

/// <summary>
/// 后台服务：连接网关后轮询 device.pair.list 并自动批准所有待审批的配对请求。
/// Singleton 生命周期：作为 <see cref="BackgroundService"/> 由 Host 管理，持有去重状态。
/// </summary>
public sealed class AutoApproveService : BackgroundService
{
    private readonly GatewayClient _client;
    private readonly TimeSpan _pollInterval;
    private readonly ConcurrentDictionary<string, byte> _approvedIds = new();

    public AutoApproveService(GatewayClient client, IOptions<AutoApproveOptions> options)
    {
        _client = client;
        _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _client.ConnectAsync(stoppingToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("NOT_PAIRED", StringComparison.OrdinalIgnoreCase))
        {
            Log.Error("当前设备未被批准，请先在 Gateway 控制面板中手动批准此设备");
            return;
        }
        catch (Exception ex)
        {
            Log.Error($"连接失败: {ex.Message}");
            return;
        }

        Log.Success("Connected!");
        Log.Info($"自动审批服务已启动，轮询间隔: {_pollInterval.TotalSeconds}s");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (TimeoutException)
            {
                Log.Warn("device.pair.list 请求超时，将在下次轮询重试");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("NOT_PAIRED"))
            {
                Log.Error("请先在 Gateway 控制面板中手动批准此服务自身，然后重新启动");
                return;
            }
            catch (Exception ex)
            {
                Log.Error($"轮询异常: {ex.Message}");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
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
