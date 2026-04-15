using System.Linq;
using System.Text.Json;
using OpenClaw.Core.Helpers;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="ChatHistoryNormalizer"/> 的单元测试：数组校验、静默行过滤、清洗规则与超大消息占位。
/// </summary>
public sealed class ChatHistoryNormalizerTests
{
    /// <summary>
    /// 非数组输入应返回空列表。
    /// </summary>
    [Fact]
    public void Normalize_non_array_returns_empty()
    {
        using var doc = JsonDocument.Parse("""{"x":1}""");
        Assert.Empty(ChatHistoryNormalizer.Normalize(doc.RootElement));
    }

    /// <summary>
    /// assistant 且内容为 NO_REPLY 的行应被过滤。
    /// </summary>
    [Fact]
    public void Normalize_filters_silent_assistant_rows()
    {
        using var doc = JsonDocument.Parse("""[{"role":"assistant","content":"NO_REPLY"},{"role":"user","content":"hi"}]""");
        var list = ChatHistoryNormalizer.Normalize(doc.RootElement);
        Assert.Single(list);
        Assert.Equal("user", list[0].GetProperty("role").GetString());
    }

    /// <summary>
    /// <see cref="ChatHistoryNormalizer.CleanContent"/> 应移除指令块与常见控制 token。
    /// </summary>
    [Fact]
    public void CleanContent_strips_directives_and_tokens()
    {
        var raw = "<directive>x</directive>hello<|im_end|>";
        var cleaned = ChatHistoryNormalizer.CleanContent(raw);
        Assert.Equal("hello", cleaned);
    }

    /// <summary>
    /// 超大文本应被替换为占位说明。
    /// </summary>
    [Fact]
    public void Normalize_replaces_oversize_content()
    {
        var big = new string('a', 120);
        using var doc = JsonDocument.Parse($$"""[{"role":"user","content":"{{big}}"}]""");
        var list = ChatHistoryNormalizer.Normalize(doc.RootElement, oversizeThreshold: 50);
        Assert.Single(list);
        var text = list[0].GetProperty("content").GetString();
        Assert.Contains("已省略", text ?? "");
    }

    /// <summary>
    /// content 为对象数组时应提取首个 text 部件。
    /// </summary>
    [Fact]
    public void Normalize_extracts_text_from_content_parts()
    {
        const string json = """[{"role":"user","content":[{"type":"text","text":"from-part <directive>x</directive>"}]}]""";
        using var doc = JsonDocument.Parse(json);
        var list = ChatHistoryNormalizer.Normalize(doc.RootElement);
        Assert.Single(list);
        Assert.Equal("from-part", list[0].GetProperty("content").GetString());
    }

    /// <summary>
    /// <see cref="ChatHistoryNormalizer.IsSilentAssistantRow"/> 对非 assistant 或非静默 token 应返回 false。
    /// </summary>
    [Fact]
    public void IsSilentAssistantRow_only_matches_assistant_tokens()
    {
        Assert.False(ChatHistoryNormalizer.IsSilentAssistantRow("user", "NO_REPLY"));
        Assert.False(ChatHistoryNormalizer.IsSilentAssistantRow("assistant", "hello"));
        Assert.True(ChatHistoryNormalizer.IsSilentAssistantRow("assistant", " no_reply "));
    }

    /// <summary>
    /// <see cref="ChatHistoryNormalizer.NormalizeChatHistoryPayload"/> 应对根对象下的 <c>messages</c> 数组做归一化。
    /// </summary>
    [Fact]
    public void NormalizeChatHistoryPayload_normalizes_messages_property()
    {
        using var doc = JsonDocument.Parse(
            """{"sessionKey":"k","messages":[{"role":"assistant","content":"NO_REPLY"},{"role":"user","content":"hi"}]}""");
        var next = ChatHistoryNormalizer.NormalizeChatHistoryPayload(doc.RootElement);
        Assert.True(next.TryGetProperty("messages", out var msgs));
        var rows = msgs.EnumerateArray().ToList();
        Assert.Single(rows);
        Assert.Equal("user", rows[0].GetProperty("role").GetString());
    }
}
