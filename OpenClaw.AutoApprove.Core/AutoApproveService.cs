using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Client;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.AutoApprove.Core;

/// <summary>
/// 后台服务：连接网关后轮询 <see cref="GatewayConstants.Methods.DevicePairList"/> 并自动批准所有待审批的配对请求。
/// Singleton 生命周期：作为 <see cref="BackgroundService"/> 由 Host 管理，持有去重状态。
/// </summary>
public sealed class AutoApproveService : BackgroundService
{
    private readonly GatewayClient _client;
    private readonly TimeSpan _pollInterval;
    private readonly ConcurrentDictionary<string, byte> _approvedIds = new();

    /// <summary>
    /// 初始化自动审批服务，注入网关客户端和配置选项。
    /// 从配置中读取轮询间隔秒数，转换为 <see cref="TimeSpan"/> 供轮询循环使用。
    /// </summary>
    /// <param name="client">网关客户端，用于发送配对列表查询和批准请求</param>
    /// <param name="options">自动审批配置选项（轮询间隔等）</param>
    public AutoApproveService(GatewayClient client, IOptions<AutoApproveOptions> options)
    {
        _client = client;
        _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
    }

    /// <summary>
    /// 后台服务主执行入口，由 <see cref="BackgroundService"/> 框架调用。
    /// 执行流程：
    /// 1. 建立到网关的 WebSocket 连接（若自身设备未配对则退出）
    /// 2. 进入无限轮询循环，每隔 <see cref="_pollInterval"/> 调用一次 <see cref="PollPendingAsync"/>
    /// 3. 轮询中遇到的异常按类型处理：取消则退出、超时则跳过、NOT_PAIRED 则终止、其他异常记录后继续
    /// 4. 收到停止信号（<paramref name="stoppingToken"/> 取消）时优雅退出
    /// </summary>
    /// <param name="stoppingToken">Host 停止时触发的取消令牌</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _client.ConnectAsync(stoppingToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains(GatewayConstants.ErrorCodes.NotPaired, StringComparison.OrdinalIgnoreCase))
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
                Log.Warn($"{GatewayConstants.Methods.DevicePairList} 请求超时，将在下次轮询重试");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains(GatewayConstants.ErrorCodes.NotPaired))
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

    /// <summary>
    /// 单次轮询：查询网关的待审批设备配对列表并逐一自动批准。
    /// 流程：
    /// 1. 调用 device.pair.list 获取配对请求列表
    /// 2. 检查响应状态，若自身未配对则抛出异常终止服务
    /// 3. 反序列化 pending 数组，过滤已处理的请求（通过 <see cref="_approvedIds"/> 去重）
    /// 4. 对每个新的待审批请求调用 <see cref="ApproveAsync"/> 执行批准
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <exception cref="InvalidOperationException">当服务自身未被批准配对时抛出</exception>
    private async Task PollPendingAsync(CancellationToken ct)
    {
        var resp = await _client.SendRequestAsync(
            GatewayConstants.Methods.DevicePairList,
            new { },
            ct);

        if (!resp.Ok)
        {
            var errorText = resp.Error?.GetRawText() ?? "unknown";

            if (errorText.Contains(GatewayConstants.ErrorCodes.NotPaired, StringComparison.OrdinalIgnoreCase))
            {
                Log.Error($"当前服务自身尚未被批准配对（{GatewayConstants.ErrorCodes.NotPaired}）");
                throw new InvalidOperationException($"{GatewayConstants.ErrorCodes.NotPaired}: 服务自身未被批准，无法继续运行");
            }

            Log.Error($"{GatewayConstants.Methods.DevicePairList} 失败: {errorText}");
            return;
        }

        if (resp.Payload is not { } payload)
        {
            Log.Debug($"{GatewayConstants.Methods.DevicePairList} 返回空 payload");
            return;
        }

        var pairList = JsonSerializer.Deserialize<PairListResponse>(payload.GetRawText(), JsonDefaults.SerializerOptions);
        if (pairList is null)
        {
            Log.Warn($"{GatewayConstants.Methods.DevicePairList} 反序列化失败");
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

    /// <summary>
    /// 批准单个设备配对请求。调用 device.pair.approve 接口向网关发送批准指令。
    /// 成功时记录设备详情日志；失败或异常时从 <see cref="_approvedIds"/> 中移除该请求 ID，
    /// 使其在下次轮询中可被重新尝试。
    /// </summary>
    /// <param name="request">待批准的配对请求，包含 requestId、deviceId 等信息</param>
    /// <param name="ct">取消令牌</param>
    private async Task ApproveAsync(PairRequest request, CancellationToken ct)
    {
        var label = !string.IsNullOrEmpty(request.DeviceId)
            ? request.DeviceId[..Math.Min(request.DeviceId.Length, 16)]
            : request.RequestId[..Math.Min(request.RequestId.Length, 16)];

        try
        {
            var resp = await _client.SendRequestAsync(
                GatewayConstants.Methods.DevicePairApprove,
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

    /// <summary>
    /// 从配对请求中提取设备附加信息，拼接为日志友好的描述字符串。
    /// 包含 clientId、platform、ip 等非空字段，格式如 " (client=xxx, platform=macintel, ip=1.2.3.4)"。
    /// 所有字段均为空时返回空字符串。
    /// </summary>
    /// <param name="request">配对请求对象</param>
    /// <returns>格式化的设备详情字符串，或空字符串</returns>
    private static string BuildDeviceDetail(PairRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(request.ClientId)) parts.Add($"client={request.ClientId}");
        if (!string.IsNullOrEmpty(request.Platform)) parts.Add($"platform={request.Platform}");
        if (!string.IsNullOrEmpty(request.Ip)) parts.Add($"ip={request.Ip}");
        return parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "";
    }
}
