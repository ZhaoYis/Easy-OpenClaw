namespace OpenClaw.Core.Logging;

/// <summary>
/// Coloured console logger with chat-aware output routing.
/// During active conversation (user input + AI streaming), system logs
/// are deferred and flushed only at safe points.
/// </summary>
public static class Log
{
    private static readonly Lock SyncRoot = new();

    private static bool _suppressSystemLogs;
    private static readonly List<(string level, ConsoleColor color, string msg)> _deferred = [];

    // ─── System Logging ────────────────────────────────────

    /// <summary>输出 INFO 级别系统日志（青色）；在 <see cref="BeginConversationTurn"/> 期间会进入延迟队列。</summary>
    /// <param name="message">日志正文</param>
    public static void Info(string message) => Write("INFO", ConsoleColor.Cyan, message);

    /// <summary>输出 WARN 级别系统日志（黄色）。</summary>
    public static void Warn(string message) => Write("WARN", ConsoleColor.Yellow, message);

    /// <summary>输出 ERR 级别系统日志（红色）。</summary>
    public static void Error(string message) => Write("ERR ", ConsoleColor.Red, message);

    /// <summary>输出成功提示（绿色）。</summary>
    public static void Success(string message) => Write(" OK ", ConsoleColor.Green, message);

    /// <summary>输出调试信息（深灰）。</summary>
    public static void Debug(string message) => Write("DBG ", ConsoleColor.DarkGray, message);

    /// <summary>输出网关/业务事件行（品红）；<paramref name="detail"/> 为空时仅打印事件名。</summary>
    /// <param name="name">事件名或标签</param>
    /// <param name="detail">可选详情，会与名称用 “→” 连接</param>
    public static void Event(string name, string detail = "")
        => Write("EVT ", ConsoleColor.Magenta, string.IsNullOrEmpty(detail) ? name : $"{name} → {detail}");

    // ─── Chat-Aware Suppression ────────────────────────────

    /// <summary>
    /// 标记新一轮用户对话开始：后续 <see cref="Info"/> 等系统日志暂存于内存，避免打断聊天 UI。
    /// </summary>
    public static void BeginConversationTurn()
    {
        lock (SyncRoot)
        {
            _suppressSystemLogs = true;
        }
    }

    /// <summary>
    /// 结束当前对话轮次：恢复系统日志输出并刷新本轮延迟的日志。
    /// </summary>
    public static void EndConversationTurn()
    {
        lock (SyncRoot)
        {
            _suppressSystemLogs = false;
            FlushDeferred();
        }
    }

    // ─── Chat UI Primitives ────────────────────────────────

    /// <summary>在控制台同一行流式输出助手增量文本（白色）。</summary>
    /// <param name="delta">本次收到的文本片段</param>
    public static void StreamDelta(string delta)
    {
        lock (SyncRoot)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(delta);
            Console.ResetColor();
        }
    }

    /// <summary>打印用户输入提示符（“You ›”）。</summary>
    public static void PrintUserPrompt()
    {
        lock (SyncRoot)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  ▎");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("You ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("› ");
            Console.ResetColor();
        }
    }

    /// <summary>在新行打印助手标题行（含 “thinking...” 占位）。</summary>
    public static void PrintAssistantHeader()
    {
        lock (SyncRoot)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  ▎");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("AI ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("› ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("thinking...");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// 清除当前行上的 “thinking...” 占位，并重新打印助手前缀（“AI ›”），准备输出正文。
    /// </summary>
    public static void ReplaceThinkingWithContent()
    {
        lock (SyncRoot)
        {
            try
            {
                var width = Math.Max(Console.WindowWidth, 40);
                Console.Write("\r");
                Console.Write(new string(' ', width - 1));
                Console.Write("\r");
            }
            catch
            {
                Console.Write("\r                                                            \r");
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  ▎");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("AI ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("› ");
            Console.ResetColor();
        }
    }

    /// <summary>打印本轮对话耗时（秒，一位小数）。</summary>
    /// <param name="elapsedMs">Stopwatch 等得到的毫秒数</param>
    public static void PrintTurnFooter(long elapsedMs)
    {
        lock (SyncRoot)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"       {elapsedMs / 1000.0:F1}s");
            Console.ResetColor();
            Console.WriteLine();
        }
    }

    /// <summary>以红色打印聊天相关错误，并换行分隔。</summary>
    public static void PrintChatError(string message)
    {
        lock (SyncRoot)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"       {message}");
            Console.ResetColor();
            Console.WriteLine();
        }
    }

    // ─── Urgent (bypass suppression) ──────────────────────

    /// <summary>
    /// 紧急日志：绕过聊天抑制机制，立即输出到控制台。
    /// 仅用于健康状态变更等不可延迟的关键通知。
    /// </summary>
    public static void Urgent(string level, ConsoleColor color, string message)
    {
        lock (SyncRoot)
        {
            WriteImmediate(level, color, message);
        }
    }

    /// <summary>
    /// 诊断跟踪日志：输出到 <see cref="System.Diagnostics.Debug"/>，
    /// 不写入主控制台，不受聊天抑制影响，仅在附加调试器时可见。
    /// 适用于后台服务（如健康监控）的高频轮询日志。
    /// </summary>
    public static void Trace(string level, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{level}] {message}");
    }

    // ─── Internals ─────────────────────────────────────────

    /// <summary>
    /// 若处于对话抑制期则入队，否则立即带时间戳输出一行日志。
    /// </summary>
    private static void Write(string level, ConsoleColor color, string message)
    {
        lock (SyncRoot)
        {
            if (_suppressSystemLogs)
            {
                _deferred.Add((level, color, message));
                return;
            }
            WriteImmediate(level, color, message);
        }
    }

    /// <summary>不经抑制判断，直接写入控制台一行。</summary>
    private static void WriteImmediate(string level, ConsoleColor color, string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
        Console.ForegroundColor = color;
        Console.Write($"[{level}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    /// <summary>按顺序输出并清空延迟队列。</summary>
    private static void FlushDeferred()
    {
        if (_deferred.Count == 0) return;

        foreach (var (level, color, msg) in _deferred)
            WriteImmediate(level, color, msg);
        _deferred.Clear();
    }
}
