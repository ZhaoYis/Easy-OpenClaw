using System.Diagnostics;
using OpenClaw.Core.Client;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

// ─── Configuration ──────────────────────────────────────

var config = AppConfigHelper.LoadFromBaseDirectory<AppConfig>();

var gatewayUrl = Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY_URL") ?? config.GatewayUrl;
var gatewayToken = Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY_TOKEN") ?? config.GatewayToken;

var stateDir = Environment.GetEnvironmentVariable("OPENCLAW_STATE_DIR")
               ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw-client");

var options = new GatewayOptions
{
    Url = gatewayUrl,
    Token = gatewayToken,
    KeyFilePath = Path.Combine(stateDir, "device.key"),
    DeviceTokenFilePath = Path.Combine(stateDir, "device.token"),
};

PrintBanner();

await using var client = new GatewayClient(options);
using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Log.Warn("收到 Ctrl+C，正在关闭...");
};

// ─── Chat Turn State ────────────────────────────────────

var sw = new Stopwatch();
var turnComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

// ─── Register ALL Event Handlers via Subscriber ─────────

var eventSubscriber = new GatewayEventSubscriber(client);

eventSubscriber.FirstDeltaReceived += () => { Log.ReplaceThinkingWithContent(); };

eventSubscriber.AgentDeltaReceived += delta => { Log.StreamDelta(delta); };

eventSubscriber.ChatTurnCompleted += () =>
{
    sw.Stop();
    Log.PrintTurnFooter(sw.ElapsedMilliseconds);
    Log.EndConversationTurn();
    turnComplete.TrySetResult();
};

eventSubscriber.ShutdownReceived += _ => { cts.Cancel(); };

eventSubscriber.RegisterAll();

// ─── Connect (auto-approval retry) ──────────────────────

try
{
    await client.ConnectWithRetryAsync(cts.Token);
}
catch (OperationCanceledException)
{
    return 1;
}
catch (Exception ex)
{
    Log.Error($"连接失败: {ex.Message}");
    return 1;
}

// ─── Post-connect: Probe key methods & print summary ────

Log.Info("── 连接成功，开始探测可用方法 ──");

try
{
    await client.HealthAsync(cts.Token);
    await client.StatusAsync(cts.Token);
    await client.GatewayIdentityGetAsync(cts.Token);
    await client.ModelsListAsync(cts.Token);
    await client.AgentsListAsync(cts.Token);
    await client.SessionsListAsync(cts.Token);
    await client.ChannelsStatusAsync(cts.Token);
    await client.TtsStatusAsync(cts.Token);
    await client.SkillsStatusAsync(cts.Token);
    await client.CronListAsync(cts.Token);
    await client.NodeListAsync(cts.Token);
    await client.DevicePairListAsync(cts.Token);
    await client.NodePairListAsync(cts.Token);
    await client.ExecApprovalsGetAsync(cts.Token);
    await client.VoicewakeGetAsync(cts.Token);
    await client.LastHeartbeatAsync(cts.Token);
    await client.UsageStatusAsync(cts.Token);
    await client.TalkConfigAsync(cts.Token);
    await client.AgentIdentityGetAsync(cts.Token);
    await client.SystemPresenceAsync(cts.Token);
    await client.ToolsCatalogAsync(cts.Token);
    await client.ConfigSchemaAsync(cts.Token);
    await client.CronStatusAsync(cts.Token);
    await client.DoctorMemoryStatusAsync(cts.Token);
    await client.TtsProvidersAsync(cts.Token);
    await client.UsageCostAsync(cts.Token);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Log.Warn($"方法探测部分失败（不影响聊天）: {ex.Message}");
}

Log.Info("── 方法探测完成 ──");

// ─── Session Info ───────────────────────────────────────

var sessionKey = client.HelloOk?.Snapshot?.SessionDefaults?.MainSessionKey ?? GatewayConstants.DefaultSessionKey;
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine($"  Session: {sessionKey}");
Console.WriteLine($"  可用方法: {client.AvailableMethods.Count} 个  可用事件: {client.AvailableEvents.Count} 个");
Console.WriteLine($"  输入消息开始对话  /quit 退出  Ctrl+C 中断");
Console.WriteLine();
Console.ResetColor();

// ─── Conversation Loop ─────────────────────────────────

while (!cts.Token.IsCancellationRequested)
{
    Log.BeginConversationTurn();
    Log.PrintUserPrompt();

    string? input;
    try
    {
        input = Console.ReadLine();
    }
    catch (OperationCanceledException)
    {
        Log.EndConversationTurn();
        break;
    }

    if (input is null || input.Trim().Equals("/quit", StringComparison.OrdinalIgnoreCase))
    {
        Log.EndConversationTurn();
        break;
    }

    if (string.IsNullOrWhiteSpace(input))
        continue;

    turnComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    sw.Restart();

    Log.PrintAssistantHeader();

    try
    {
        var resp = await client.ChatAsync(input.Trim(), ct: cts.Token);

        if (!resp.Ok)
        {
            Log.PrintChatError($"chat.send 失败: {resp.Error?.GetRawText() ?? "unknown error"}");
            Log.EndConversationTurn();
            continue;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
            await turnComplete.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cts.Token.IsCancellationRequested)
        {
            sw.Stop();
            Log.PrintTurnFooter(sw.ElapsedMilliseconds);
            Log.EndConversationTurn();
        }
    }
    catch (TimeoutException)
    {
        Log.PrintChatError("请求超时");
        Log.EndConversationTurn();
    }
    catch (OperationCanceledException)
    {
        Log.EndConversationTurn();
        break;
    }
    catch (Exception ex)
    {
        Log.PrintChatError($"发送失败: {ex.Message}");
        Log.EndConversationTurn();
    }
}

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("  再见!");
Console.ResetColor();
Console.WriteLine();
return 0;

// ─── Helpers ────────────────────────────────────────────

static void PrintBanner()
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("  OpenClaw");
    Console.ForegroundColor = ConsoleColor.White;
    Console.Write(" Gateway Client");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  v1.0.0 · .NET 10");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  ─────────────────────────────────────────────────");
    Console.ResetColor();
    Console.WriteLine();
}

sealed record AppConfig
{
    public string GatewayUrl { get; init; } = GatewayConstants.DefaultGatewayUrl;
    public string GatewayToken { get; init; } = "YOUR_GATEWAY_TOKEN";
}