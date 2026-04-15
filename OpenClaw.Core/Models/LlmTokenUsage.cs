namespace OpenClaw.Core.Models;

/// <summary>
/// 单次或累计的 LLM token 用量，便于账单、配额与统计（与 OpenAI 等接口的 prompt / completion 划分一致）。
/// </summary>
public readonly record struct LlmTokenUsage(int PromptTokens, int CompletionTokens)
{
    /// <summary>prompt_tokens + completion_tokens</summary>
    public int TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>由上游 API 返回的 usage 字段构造（如 OpenAI <c>usage.prompt_tokens</c> / <c>completion_tokens</c>）。</summary>
    public static LlmTokenUsage FromProvider(int promptTokens, int completionTokens) =>
        new(PromptTokens: Math.Max(0, promptTokens), CompletionTokens: Math.Max(0, completionTokens));

    public static LlmTokenUsage operator +(LlmTokenUsage a, LlmTokenUsage b) =>
        new(a.PromptTokens + b.PromptTokens, a.CompletionTokens + b.CompletionTokens);
}
