using System.Text.Json;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 使用传入的 <see cref="GatewayOptions"/> 中的文件路径异步读写 DeviceToken 与 scopes，行为与早期内嵌在 <see cref="GatewayClient"/> 中的实现一致。
/// </summary>
public sealed class FileGatewayClientStateStore : IGatewayClientStateStore
{
    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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
