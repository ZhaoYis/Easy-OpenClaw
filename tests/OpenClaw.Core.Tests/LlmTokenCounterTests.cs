using OpenClaw.Core.Helpers;
using OpenClaw.Core.Models;
using Xunit;

namespace OpenClaw.Core.Tests;

public sealed class LlmTokenCounterTests
{
    [Fact]
    public void CountTokens_empty_or_null_is_zero()
    {
        Assert.Equal(0, LlmTokenCounter.CountTokens(null));
        Assert.Equal(0, LlmTokenCounter.CountTokens(""));
        Assert.Equal(0, LlmTokenCounter.CountTokens(ReadOnlySpan<char>.Empty));
    }

    [Fact]
    public void CountTokens_matches_EncodeToIds_length()
    {
        var tokenizer = LlmTokenCounter.DefaultCl100kTokenizer;
        const string s = "OpenClaw：混合 ASCII 与 emoji 🦞 123";
        var expected = tokenizer.EncodeToIds(s, considerPreTokenization: true, considerNormalization: true).Count;
        Assert.Equal(expected, LlmTokenCounter.CountTokens(s));
        Assert.Equal(expected, LlmTokenCounter.CountTokens(s.AsSpan(), tokenizer));
    }

    [Fact]
    public void EstimateUsage_splits_prompt_and_completion()
    {
        var u = LlmTokenCounter.EstimateUsage("a", "bb");
        Assert.Equal(LlmTokenCounter.CountTokens("a"), u.PromptTokens);
        Assert.Equal(LlmTokenCounter.CountTokens("bb"), u.CompletionTokens);
        Assert.Equal(u.PromptTokens + u.CompletionTokens, u.TotalTokens);
    }

    [Fact]
    public void LlmTokenUsage_FromProvider_and_plus()
    {
        var a = LlmTokenUsage.FromProvider(10, 3);
        Assert.Equal(10, a.PromptTokens);
        Assert.Equal(3, a.CompletionTokens);
        Assert.Equal(13, a.TotalTokens);

        var b = LlmTokenUsage.FromProvider(-5, -1);
        Assert.Equal(0, b.PromptTokens);
        Assert.Equal(0, b.CompletionTokens);

        var c = a + LlmTokenUsage.FromProvider(1, 2);
        Assert.Equal(11, c.PromptTokens);
        Assert.Equal(5, c.CompletionTokens);
        Assert.Equal(16, c.TotalTokens);
    }
}
