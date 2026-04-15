using System.Text.Json;
using OpenClaw.Core.Client;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 在 Hub 桥接建连时若 <c>state</c> 为 <see cref="OpenClawSignalRGatewayHubBridgeContext"/>，
/// 将 DeviceToken / scopes 持久化到 <c>OPENCLAW_STATE_DIR</c> 下按 <see cref="OpenClawSignalRGatewayHubBridgeContext.UserId"/> 分目录的文件；
/// 否则回退到 <see cref="GatewayOptions"/> 中的全局路径（与 <see cref="FileGatewayClientStateStore"/> 行为一致）。
/// </summary>
public class OpenClawSignalRGatewayClientStateStore : IGatewayClientStateStore
{
    /// <summary>在需要用到持久化 DeviceToken 时调用（如每次建连开始前），无缓存则返回 null。</summary>
    /// <param name="state">为 <see cref="OpenClawSignalRGatewayHubBridgeContext"/> 时使用每用户路径；否则用全局路径。</param>
    /// <param name="gatewayOptions">全局 <see cref="GatewayOptions.DeviceTokenFilePath"/>；桥接模式下可能回退到此路径。</param>
    /// <param name="cancellationToken">取消读取。</param>
    public async Task<string?> LoadDeviceTokenAsync(object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = this.GetDeviceTokenFilePath(state, gatewayOptions.DeviceTokenFilePath);
            if (path is null || !File.Exists(path))
                return null;

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
    /// <param name="state">桥接上下文且含用户 id 时写入分用户目录。</param>
    /// <param name="gatewayOptions">全局路径配置。</param>
    /// <param name="cancellationToken">取消写入。</param>
    public async Task SaveDeviceTokenAsync(string token, object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = this.GetDeviceTokenFilePath(state, gatewayOptions.DeviceTokenFilePath);
            if (path is null)
                return;

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
    /// <param name="state">为桥接上下文时使用每用户路径；否则用全局路径。</param>
    /// <param name="gatewayOptions">全局 <see cref="GatewayOptions.DeviceScopesFilePath"/>。</param>
    /// <param name="cancellationToken">取消读取。</param>
    public async Task<string[]?> LoadDeviceScopesAsync(object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = this.GetDeviceScopesFilePath(state, gatewayOptions.DeviceScopesFilePath);
            if (path is null || !File.Exists(path))
                return null;

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
    /// <param name="state">桥接上下文且含用户 id 时写入分用户目录。</param>
    /// <param name="gatewayOptions">全局路径配置。</param>
    /// <param name="cancellationToken">取消写入。</param>
    public async Task SaveDeviceScopesAsync(string[] scopes, object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = this.GetDeviceScopesFilePath(state, gatewayOptions.DeviceScopesFilePath);
            if (path is null)
                return;

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

    /// <summary>桥接上下文下返回每用户 token 路径；否则返回 <paramref name="defaultPath"/>（可为 null）。</summary>
    private string? GetDeviceTokenFilePath(object? state, string? defaultPath)
    {
        if (state is OpenClawSignalRGatewayHubBridgeContext context)
        {
            if (string.IsNullOrEmpty(context.UserId))
                return defaultPath;

            var stateDir = Environment.GetEnvironmentVariable("OPENCLAW_STATE_DIR")
                           ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw-client");
            return Path.Combine(stateDir, "userDeviceTokens", context.UserId, GatewayConstants.FileNames.DeviceToken);
        }

        return defaultPath;
    }

    /// <summary>桥接上下文下返回每用户 scopes 路径；否则返回 <paramref name="defaultPath"/>（可为 null）。</summary>
    private string? GetDeviceScopesFilePath(object? state, string? defaultPath)
    {
        if (state is OpenClawSignalRGatewayHubBridgeContext context)
        {
            if (string.IsNullOrEmpty(context.UserId))
                return defaultPath;

            var stateDir = Environment.GetEnvironmentVariable("OPENCLAW_STATE_DIR")
                           ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw-client");

            return Path.Combine(stateDir, "userDeviceScopes", context.UserId, GatewayConstants.FileNames.DeviceScopes);
        }

        return defaultPath;
    }
}