using System.Text.Json;
using System.Text.RegularExpressions;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Helpers;

/// <summary>
/// 在将用户侧文本通过本库发往**上游 OpenClaw 网关**之前做统一清洗，降低提示词注入与伪造控制/工具载荷的风险。
/// 在 <see cref="Client.GatewayClient"/> 出站 RPC 路径上自动应用（可通过 <see cref="Models.GatewayOptions.SanitizeOutboundUserMessages"/> 关闭）。
/// </summary>
/// <remarks>
/// 与 <see cref="ChatHistoryNormalizer.CleanContent"/> 一致的部分：剥离指令标签、工具调用 XML、模型控制 token；
/// 另移除常见不可见字符与双向文本控制符，减轻混淆与越权指令伪装。
/// </remarks>
public static partial class GatewayOutboundMessageSanitizer
{
    /// <summary>
    /// 移除 C0/C1 控制符、零宽与双向覆盖等 Unicode，避免用户文本干扰下游解析或展示。
    /// </summary>
    [GeneratedRegex(
        @"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F\u0080-\u009F\u200B-\u200F\u202A-\u202E\u2060-\u2064\u2066-\u2069\uFEFF]",
        RegexOptions.Compiled)]
    private static partial Regex InvisibleAndBidiRegex();

    /// <summary>
    /// 对单条将进入模型上下文的用户文本做出站清洗。
    /// </summary>
    public static string SanitizeOutboundUserText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? "";

        var stripped = InvisibleAndBidiRegex().Replace(text, "");
        var cleaned = ChatHistoryNormalizer.CleanContent(stripped);
        return cleaned ?? "";
    }

    /// <summary>
    /// 按 RPC 方法名清洗 <c>params</c> 中已知的用户文本字段（若存在且为 JSON 字符串）。
    /// </summary>
    public static JsonElement SanitizeRpcParams(string method, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
            return parameters;

        var field = ResolveSanitizedField(method);
        if (field is null)
            return parameters;

        if (!parameters.TryGetProperty(field, out var v) || v.ValueKind != JsonValueKind.String)
            return parameters;

        var raw = v.GetString() ?? "";
        var sanitized = SanitizeOutboundUserText(raw);
        if (sanitized == raw)
            return parameters;

        return ReplaceStringProperty(parameters, field, sanitized);
    }

    public static string? ResolveSanitizedField(string method)
    {
        if (string.Equals(method, GatewayConstants.Methods.ChatSend, StringComparison.Ordinal))
            return "message";
        if (string.Equals(method, GatewayConstants.Methods.SessionsSend, StringComparison.Ordinal))
            return "message";
        if (string.Equals(method, GatewayConstants.Methods.SessionsSteer, StringComparison.Ordinal))
            return "message";
        if (string.Equals(method, GatewayConstants.Methods.ChatInject, StringComparison.Ordinal))
            return "content";
        if (string.Equals(method, GatewayConstants.Methods.Send, StringComparison.Ordinal))
            return "text";
        return null;
    }

    private static JsonElement ReplaceStringProperty(JsonElement root, string propName, string newValue)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.NameEquals(propName))
                    writer.WriteString(propName, newValue);
                else
                    prop.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        ms.Position = 0;
        return JsonDocument.Parse(ms).RootElement.Clone();
    }
}
