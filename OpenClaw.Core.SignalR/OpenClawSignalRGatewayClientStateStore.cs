using System.Text.Json;
using OpenClaw.Core.Client;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.SignalR;

public class OpenClawSignalRGatewayClientStateStore : IGatewayClientStateStore
{
    /// <inheritdoc />
    public async Task<string?> LoadDeviceTokenAsync(object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = await File.ReadAllTextAsync(this.GetDeviceTokenFilePath(state, gatewayOptions.DeviceTokenFilePath), cancellationToken).ConfigureAwait(false);
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
        try
        {
            var path = this.GetDeviceTokenFilePath(state, gatewayOptions.DeviceTokenFilePath);
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
        try
        {
            var path = this.GetDeviceScopesFilePath(state, gatewayOptions.DeviceScopesFilePath);
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
        try
        {
            var path = this.GetDeviceScopesFilePath(state, gatewayOptions.DeviceScopesFilePath);
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

    private string GetDeviceTokenFilePath(object? state, string? defaultPath)
    {
        if (state is OpenClawSignalRGatewayHubBridgeContext context)
        {
            var stateDir = Environment.GetEnvironmentVariable("OPENCLAW_STATE_DIR")
                           ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw-client");
            return Path.Combine(stateDir, "userDeviceTokens", context.UserId, GatewayConstants.FileNames.DeviceToken);
        }

        return defaultPath;
    }

    private string GetDeviceScopesFilePath(object? state, string? defaultPath)
    {
        if (state is OpenClawSignalRGatewayHubBridgeContext context)
        {
            var stateDir = Environment.GetEnvironmentVariable("OPENCLAW_STATE_DIR")
                           ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw-client");

            return Path.Combine(stateDir, "userDeviceScopes", context.UserId, GatewayConstants.FileNames.DeviceScopes);
        }

        return defaultPath;
    }
}