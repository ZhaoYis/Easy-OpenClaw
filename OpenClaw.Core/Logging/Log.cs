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
