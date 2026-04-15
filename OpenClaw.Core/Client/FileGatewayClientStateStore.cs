using System.Text.Json;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 使用传入的 <see cref="GatewayOptions"/> 中的文件路径异步读写 DeviceToken 与 scopes，行为与早期内嵌在 <see cref="GatewayClient"/> 中的实现一致。
/// </summary>
public sealed class FileGatewayClientStateStore : IGatewayClientStateStore
{
    /// <summary>在需要用到持久化 DeviceToken 时调用（如每次建连开始前），无缓存则返回 null。</summary>
    /// <param name="state">调用方上下文；本实现忽略。</param>
    /// <param name="gatewayOptions">路径取自 <see cref="GatewayOptions.DeviceTokenFilePath"/>。</param>
    /// <param name="cancellationToken">取消读取。</param>
    public async Task<string?> LoadDeviceTokenAsync(object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default)
    {
        _ = state;
        var path = gatewayOptions.DeviceTokenFilePath;
        if (path is null || !File.Exists(path)) return null;

        try
        {
            var raw = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var token = raw.Trim();
            return string.IsNullOrEmpty(token) ? null : token;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>在握手获得新 DeviceToken 或 bootstrap handoff 时调用。</summary>
    /// <param name="token">要写入的令牌。</param>
    /// <param name="state">与本次建连上下文一致；本实现忽略。</param>
    /// <param name="gatewayOptions">路径取自 <see cref="GatewayOptions.DeviceTokenFilePath"/>；为 null 时不写入。</param>
    /// <param name="cancellationToken">取消写入。</param>
    public async Task SaveDeviceTokenAsync(string token, object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default)
    {
        _ = state;
        var path = gatewayOptions.DeviceTokenFilePath;
        if (path is null) return;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(path, token, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warn($"保存 deviceToken 失败: {ex.Message}");
        }
    }

    /// <summary>在需要用到持久化 scopes 时调用（如每次建连开始前），无缓存则返回 null。</summary>
    /// <param name="state">调用方上下文；本实现忽略。</param>
    /// <param name="gatewayOptions">路径取自 <see cref="GatewayOptions.DeviceScopesFilePath"/>。</param>
    /// <param name="cancellationToken">取消读取。</param>
    public async Task<string[]?> LoadDeviceScopesAsync(object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default)
    {
        _ = state;
        var path = gatewayOptions.DeviceScopesFilePath;
        if (path is null || !File.Exists(path)) return null;

        try
        {
            var json = (await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Trim();
            if (string.IsNullOrEmpty(json)) return null;
            return JsonSerializer.Deserialize<string[]>(json);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>在握手获得服务端授予的 scopes 时调用。</summary>
    /// <param name="scopes">要持久化的作用域数组。</param>
    /// <param name="state">与本次建连上下文一致；本实现忽略。</param>
    /// <param name="gatewayOptions">路径取自 <see cref="GatewayOptions.DeviceScopesFilePath"/>；为 null 时不写入。</param>
    /// <param name="cancellationToken">取消写入。</param>
    public async Task SaveDeviceScopesAsync(string[] scopes, object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default)
    {
        _ = state;
        var path = gatewayOptions.DeviceScopesFilePath;
        if (path is null) return;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(scopes);
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warn($"保存 scopes 失败: {ex.Message}");
        }
    }
}
