using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Core.Helpers;

/// <summary>
/// 全局共享的 JSON 序列化配置。避免各项目重复创建 JsonSerializerOptions 实例。
/// </summary>
public static class JsonDefaults
{
    /// <summary>
    /// 与网关 JSON 协议对齐的全局选项：camelCase、忽略 null、属性名大小写不敏感、不缩进。
    /// 用法：<c>JsonSerializer.Serialize(..., JsonDefaults.SerializerOptions)</c>。
    /// </summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
}
