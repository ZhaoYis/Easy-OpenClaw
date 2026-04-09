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

    public static void Info(string message) => Write("INFO", ConsoleColor.Cyan, message);
    public static void Warn(string message) => Write("WARN", ConsoleColor.Yellow, message);
    public static void Error(string message) => Write("ERR ", ConsoleColor.Red, message);
    public static void Success(string message) => Write(" OK ", ConsoleColor.Green, message);
    public static void Debug(string message) => Write("DBG ", ConsoleColor.DarkGray, message);
    public static void Event(string name, string detail = "")
        => Write("EVT ", ConsoleColor.Magenta, string.IsNullOrEmpty(detail) ? name : $"{name} → {detail}");

    // ─── Chat-Aware Suppression ────────────────────────────

    public static void BeginConversationTurn()
    {
        lock (SyncRoot)
        {
            _suppressSystemLogs = true;
        }
    }

    public static void EndConversationTurn()
    {
        lock (SyncRoot)
        {
            _suppressSystemLogs = false;
            FlushDeferred();
        }
    }

    // ─── Chat UI Primitives ────────────────────────────────

    public static void StreamDelta(string delta)
    {
        lock (SyncRoot)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(delta);
            Console.ResetColor();
        }
    }

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

    private static void WriteImmediate(string level, ConsoleColor color, string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
        Console.ForegroundColor = color;
        Console.Write($"[{level}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    private static void FlushDeferred()
    {
        if (_deferred.Count == 0) return;

        foreach (var (level, color, msg) in _deferred)
            WriteImmediate(level, color, msg);
        _deferred.Clear();
    }
}
