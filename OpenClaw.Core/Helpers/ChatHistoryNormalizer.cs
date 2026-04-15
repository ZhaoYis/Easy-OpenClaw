using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenClaw.Core.Helpers;

/// <summary>
/// chat.history 的客户端侧展示归一化工具。
/// 在将聊天历史渲染到 UI 之前，对原始消息数组进行以下清洗：
/// <list type="bullet">
///   <item>剥离内联指令标签（directive tags）使其不在可见文本中出现</item>
///   <item>移除纯文本中的工具调用 XML 载荷（tool_call、function_call、tool_calls、function_calls 及其截断片段）</item>
///   <item>剥离泄露的 ASCII/全角模型控制 token（如 &lt;|im_end|&gt;、&lt;|endoftext|&gt; 等）</item>
///   <item>省略纯静默 token 的 assistant 行（精确匹配 NO_REPLY / no_reply）</item>
///   <item>将超大行替换为占位符以防止 UI 崩溃</item>
/// </list>
/// </summary>
public static partial class ChatHistoryNormalizer
{
    /// <summary>超大消息的默认字符阈值，超过此长度的消息内容将被替换为占位符</summary>
    public const int DefaultOversizeThreshold = 100_000;

    /// <summary>超大消息的占位符文本模板，{0} 为原始字符数</summary>
    public const string OversizePlaceholderTemplate = "[消息过大，已省略 ({0} 字符)]";

    /// <summary>
    /// 匹配内联指令标签，如 &lt;directive&gt;...&lt;/directive&gt;、&lt;system&gt;...&lt;/system&gt; 等。
    /// 使用非贪婪模式匹配标签内容。
    /// </summary>
    [GeneratedRegex(@"<(directive|system_instruction|instruction|context|internal)>[\s\S]*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DirectiveTagsRegex();

    /// <summary>
    /// 匹配工具调用 XML 块：&lt;tool_call&gt;...&lt;/tool_call&gt;、&lt;function_call&gt;...&lt;/function_call&gt;
    /// 以及复数形式 tool_calls / function_calls。
    /// </summary>
    [GeneratedRegex(@"<(tool_calls?|function_calls?)>[\s\S]*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ToolCallXmlRegex();

    /// <summary>
    /// 匹配截断的工具调用 XML 块（开始标签存在但没有对应的关闭标签，一直到文本末尾）。
    /// </summary>
    [GeneratedRegex(@"<(tool_calls?|function_calls?)>[\s\S]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TruncatedToolCallRegex();

    /// <summary>
    /// 匹配常见的泄露模型控制 token（ASCII 和全角变体）：
    /// &lt;|im_start|&gt;、&lt;|im_end|&gt;、&lt;|im_sep|&gt;、&lt;|endoftext|&gt;、&lt;|end|&gt;、
    /// &lt;|pad|&gt;、&lt;|assistant|&gt;、&lt;|user|&gt;、&lt;|system|&gt;、[INST]、[/INST]、&lt;s&gt;、&lt;/s&gt;
    /// 以及它们的全角括号变体（＜｜...｜＞）。
    /// </summary>
    [GeneratedRegex(
        @"(<\|(?:im_start|im_end|im_sep|endoftext|end|pad|assistant|user|system)\|>)" +
        @"|(\uFF1C\uFF5C(?:im_start|im_end|im_sep|endoftext|end|pad|assistant|user|system)\uFF5C\uFF1E)" +
        @"|(\[/?INST\])" +
        @"|(</?s>)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ControlTokensRegex();

    /// <summary>
    /// 判定纯静默 token 的 assistant 行。精确匹配 NO_REPLY / no_reply（忽略前后空白）。
    /// </summary>
    private static readonly HashSet<string> SilentTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "NO_REPLY",
        "no_reply",
    };

    /// <summary>
    /// 对 chat.history 返回的原始消息数组进行展示归一化。
    /// 返回清洗后的新数组，原始数组不被修改。
    /// </summary>
    /// <param name="rawMessages">chat.history 返回的原始 JSON 消息数组</param>
    /// <param name="oversizeThreshold">超大消息字符阈值，默认 <see cref="DefaultOversizeThreshold"/></param>
    /// <returns>归一化后的消息 JSON 元素列表（已过滤静默行、清洗文本、替换超大行）</returns>
    public static List<JsonElement> Normalize(JsonElement rawMessages, int oversizeThreshold = DefaultOversizeThreshold)
    {
        if (rawMessages.ValueKind != JsonValueKind.Array)
            return [];

        var results = new List<JsonElement>();

        foreach (var msg in rawMessages.EnumerateArray())
        {
            var role = msg.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null;
            var content = ExtractContent(msg);

            if (IsSilentAssistantRow(role, content))
                continue;

            var cleaned = CleanContent(content);

            if (cleaned is not null && cleaned.Length > oversizeThreshold)
            {
                cleaned = string.Format(OversizePlaceholderTemplate, cleaned.Length);
            }

            if (cleaned != content)
            {
                var rebuilt = RebuildMessageWithContent(msg, cleaned);
                results.Add(rebuilt);
            }
            else
            {
                results.Add(msg);
            }
        }

        return results;
    }

    /// <summary>
    /// 对单条消息文本执行所有清洗规则：
    /// 移除指令标签 → 移除工具调用 XML → 移除截断的工具调用 → 移除控制 token → 去除多余空行。
    /// </summary>
    /// <param name="content">原始消息文本</param>
    /// <returns>清洗后的文本，原始为 null 时返回 null</returns>
    public static string? CleanContent(string? content)
    {
        if (content is null)
            return null;

        var result = DirectiveTagsRegex().Replace(content, "");
        result = ToolCallXmlRegex().Replace(result, "");
        result = TruncatedToolCallRegex().Replace(result, "");
        result = ControlTokensRegex().Replace(result, "");

        result = CollapseBlankLines(result);

        return result.Trim();
    }

    /// <summary>
    /// 判断是否为纯静默 token 的 assistant 行。
    /// 条件：role 为 "assistant" 且 content 去除空白后精确匹配 NO_REPLY / no_reply。
    /// </summary>
    /// <param name="role">消息角色</param>
    /// <param name="content">消息文本内容</param>
    /// <returns>如果是静默 assistant 行则返回 true</returns>
    public static bool IsSilentAssistantRow(string? role, string? content)
    {
        if (!string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
            return false;

        if (content is null)
            return false;

        var trimmed = content.Trim();
        return SilentTokens.Contains(trimmed);
    }

    /// <summary>
    /// 从消息 JSON 中提取文本内容。支持 content 为字符串或对象数组（取第一个 text 类型元素）。
    /// </summary>
    /// <param name="msg">消息 JSON 元素</param>
    /// <returns>提取到的文本内容，无法提取时返回 null</returns>
    private static string? ExtractContent(JsonElement msg)
    {
        if (!msg.TryGetProperty("content", out var contentProp))
            return null;

        if (contentProp.ValueKind == JsonValueKind.String)
            return contentProp.GetString();

        if (contentProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in contentProp.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var typeProp)
                    && string.Equals(typeProp.GetString(), "text", StringComparison.OrdinalIgnoreCase)
                    && part.TryGetProperty("text", out var textProp))
                {
                    return textProp.GetString();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 将连续多个空行折叠为单个空行。
    /// </summary>
    private static string CollapseBlankLines(string text)
    {
        return CollapseBlankLinesRegex().Replace(text, "\n\n");
    }

    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex CollapseBlankLinesRegex();

    /// <summary>
    /// 用清洗后的 content 重建消息 JSON 元素。保留原始消息的所有其他属性，仅替换 content 字段。
    /// </summary>
    /// <param name="original">原始消息 JSON 元素</param>
    /// <param name="newContent">清洗后的内容文本</param>
    /// <returns>重建后的 JSON 元素</returns>
    private static JsonElement RebuildMessageWithContent(JsonElement original, string? newContent)
    {
        using var doc = JsonDocument.Parse(original.GetRawText());
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("content"))
                {
                    writer.WritePropertyName("content");
                    if (newContent is null)
                        writer.WriteNullValue();
                    else
                        writer.WriteStringValue(newContent);
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        ms.Position = 0;
        return JsonDocument.Parse(ms).RootElement.Clone();
    }

    /// <summary>
    /// 将 <c>chat.history</c> 等服务返回的载荷归一化为适合 UI 展示的形态：
    /// 根为消息数组，或根对象下存在 <c>messages</c> 数组时对其套用 <see cref="Normalize"/>。
    /// 其他形状原样返回（克隆）。
    /// </summary>
    public static JsonElement NormalizeChatHistoryPayload(JsonElement payload, int oversizeThreshold = DefaultOversizeThreshold)
    {
        if (payload.ValueKind == JsonValueKind.Array)
            return ToJsonArrayElement(Normalize(payload, oversizeThreshold));

        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("messages", out var msgs)
            && msgs.ValueKind == JsonValueKind.Array)
        {
            var normalized = Normalize(msgs, oversizeThreshold);
            return ReplacePropertyValue(payload, "messages", ToJsonArrayElement(normalized));
        }

        return payload.Clone();
    }

    private static JsonElement ToJsonArrayElement(List<JsonElement> items)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            foreach (var e in items)
                e.WriteTo(w);
            w.WriteEndArray();
        }

        ms.Position = 0;
        return JsonDocument.Parse(ms).RootElement.Clone();
    }

    private static JsonElement ReplacePropertyValue(JsonElement root, string name, JsonElement newValue)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            foreach (var p in root.EnumerateObject())
            {
                if (p.NameEquals(name))
                {
                    w.WritePropertyName(name);
                    newValue.WriteTo(w);
                }
                else
                {
                    p.WriteTo(w);
                }
            }

            w.WriteEndObject();
        }

        ms.Position = 0;
        return JsonDocument.Parse(ms).RootElement.Clone();
    }
}