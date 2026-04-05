using System.Text.Json;

namespace OpenClaw.Core.Helpers;

/// <summary>
/// 通用的 appsettings.json 配置加载器。
/// 两个入口项目 (Gateway.Client / AutoApprove) 共用此方法，避免重复 LoadConfig 逻辑。
/// </summary>
public static class AppConfigHelper
{
    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 从 JSON 文件加载配置，文件不存在或解析失败时返回默认实例。
    /// </summary>
    public static T Load<T>(string path) where T : new()
    {
        if (!File.Exists(path))
            return new T();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, ConfigJsonOptions) ?? new T();
        }
        catch
        {
            return new T();
        }
    }

    /// <summary>
    /// 从 appsettings.json（基于 AppContext.BaseDirectory）加载配置。
    /// </summary>
    public static T LoadFromBaseDirectory<T>() where T : new()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        return Load<T>(path);
    }
}
