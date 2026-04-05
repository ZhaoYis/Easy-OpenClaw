using OpenClaw.Core.Logging;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="Log"/> 的冒烟测试：覆盖各日志与对话 UI 辅助方法（向控制台输出，不校验具体文本）。
/// </summary>
public sealed class LogTests
{
    /// <summary>
    /// 调用基础日志级别方法，确保无异常。
    /// </summary>
    [Fact]
    public void System_log_levels_do_not_throw()
    {
        Log.Info("i");
        Log.Warn("w");
        Log.Error("e");
        Log.Success("s");
        Log.Debug("d");
        Log.Event("name", "detail");
        Log.Event("nameOnly");
    }

    /// <summary>
    /// 对话抑制窗口内写入的日志应在 <see cref="Log.EndConversationTurn"/> 时冲刷。
    /// </summary>
    [Fact]
    public void Conversation_suppression_defers_then_flushes()
    {
        Log.BeginConversationTurn();
        Log.Info("deferred");
        Log.EndConversationTurn();
    }

    /// <summary>
    /// 聊天 UI 相关方法应可调用（依赖控制台句柄，测试环境通常可用）。
    /// </summary>
    [Fact]
    public void Chat_ui_primitives_do_not_throw()
    {
        Log.PrintUserPrompt();
        Log.PrintAssistantHeader();
        Log.ReplaceThinkingWithContent();
        Log.StreamDelta("x");
        Log.PrintTurnFooter(1200);
        Log.PrintChatError("err");
    }
}
