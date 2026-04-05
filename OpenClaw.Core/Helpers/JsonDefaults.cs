using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Core.Helpers;

/// <summary>
/// 全局共享的 JSON 序列化配置。避免各项目重复创建 JsonSerializerOptions 实例。
/// </summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
}
