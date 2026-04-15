using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 使用 <see cref="GatewayOptions"/> 中的文件路径读写 DeviceToken 与 scopes，行为与早期内嵌在 <see cref="GatewayClient"/> 中的实现一致。
/// </summary>
public sealed class FileGatewayClientStateStore(IOptions<GatewayOptions> options) : IGatewayClientStateStore
{
    private readonly GatewayOptions _options = options.Value;

    /// <inheritdoc />
    public string? LoadDeviceToken()
    {
        var path = _options.DeviceTokenFilePath;
        if (path is null || !File.Exists(path)) return null;

        try
        {
            var token = File.ReadAllText(path).Trim();
            return string.IsNullOrEmpty(token) ? null : token;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void SaveDeviceToken(string token)
    {
        var path = _options.DeviceTokenFilePath;
        if (path is null) return;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, token);
        }
        catch (Exception ex)
        {
            Log.Warn($"保存 deviceToken 失败: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public string[]? LoadDeviceScopes()
    {
        var path = _options.DeviceScopesFilePath;
        if (path is null || !File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path).Trim();
            if (string.IsNullOrEmpty(json)) return null;
            return JsonSerializer.Deserialize<string[]>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void SaveDeviceScopes(string[] scopes)
    {
        var path = _options.DeviceScopesFilePath;
        if (path is null) return;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(scopes));
        }
        catch (Exception ex)
        {
            Log.Warn($"保存 scopes 失败: {ex.Message}");
        }
    }
}
