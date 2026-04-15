using Microsoft.ML.Tokenizers;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Helpers;

/// <summary>
/// 基于 Tiktoken（默认 <c>cl100k_base</c>，适用于 GPT-4 / GPT-3.5-turbo 等）的文本 token 计数，
/// 用于预估上下文、本地账单试算；实际计费以服务商返回的 <see cref="LlmTokenUsage"/> 为准。
/// </summary>
/// <remarks>
/// 需引用 <c>Microsoft.ML.Tokenizers.Data.Cl100kBase</c>，以便 <see cref="TiktokenTokenizer.CreateForEncoding"/> 能解析 <c>cl100k_base</c>。
/// </remarks>
public static class LlmTokenCounter
{
    /// <summary>OpenAI 兼容模型常用的 Tiktoken 编码名。</summary>
    public const string DefaultEncodingName = "cl100k_base";

    private static readonly Lazy<Tokenizer> DefaultTokenizer = new(CreateDefaultTokenizer, LazyThreadSafetyMode.ExecutionAndPublication);

    private static Tokenizer CreateDefaultTokenizer() =>
        TiktokenTokenizer.CreateForEncoding(DefaultEncodingName, extraSpecialTokens: null, normalizer: null);

    /// <summary>默认 cl100k_base 分词器（进程内单例，可安全复用）。</summary>
    public static Tokenizer DefaultCl100kTokenizer => DefaultTokenizer.Value;

    /// <summary>使用默认编码统计一段文本的 token 数；<paramref name="text"/> 为 null 或空时返回 0。</summary>
    public static int CountTokens(string? text) => CountTokens(text, DefaultCl100kTokenizer);

    /// <summary>使用指定分词器统计 token 数。</summary>
    public static int CountTokens(string? text, Tokenizer tokenizer)
    {
        ArgumentNullException.ThrowIfNull(tokenizer);
        if (string.IsNullOrEmpty(text))
            return 0;
        return tokenizer.CountTokens(text, considerPreTokenization: true, considerNormalization: true);
    }

    /// <summary>对只读字符范围统计 token 数。</summary>
    public static int CountTokens(ReadOnlySpan<char> text, Tokenizer? tokenizer = null)
    {
        tokenizer ??= DefaultCl100kTokenizer;
        if (text.IsEmpty)
            return 0;
        return tokenizer.CountTokens(text, considerPreTokenization: true, considerNormalization: true);
    }

    /// <summary>
    /// 分别对「输入侧文本」与「输出侧文本」做本地 token 估算，便于在尚无 API usage 时试算费用。
    /// </summary>
    public static LlmTokenUsage EstimateUsage(string? promptText, string? completionText, Tokenizer? tokenizer = null)
    {
        tokenizer ??= DefaultCl100kTokenizer;
        return LlmTokenUsage.FromProvider(CountTokens(promptText, tokenizer), CountTokens(completionText, tokenizer));
    }
}
