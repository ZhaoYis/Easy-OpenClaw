using OpenClaw.AutoApprove;
using OpenClaw.Core.Client;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

// ─── Configuration ──────────────────────────────────────

var config = AppConfigHelper.LoadFromBaseDirectory<AppConfig>();

var gatewayUrl = Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY_URL") ?? config.GatewayUrl;
var gatewayToken = Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY_TOKEN") ?? config.GatewayToken;
var pollIntervalSec = config.PollIntervalSeconds;

var stateDir = Environment.GetEnvironmentVariable("OPENCLAW_STATE_DIR")
               ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw-autoapprove");

var options = new GatewayOptions
{
    Url = gatewayUrl,
    Token = gatewayToken,
    ClientId = GatewayConstants.ClientIds.Cli,
    ClientMode = GatewayConstants.ClientModes.Cli,
    Role = GatewayConstants.Roles.Operator,
    Scopes = [GatewayConstants.Scopes.Pairing, GatewayConstants.Scopes.Read],
    KeyFilePath = Path.Combine(stateDir, "device.key"),
    DeviceTokenFilePath = Path.Combine(stateDir, "device.token"),
};

PrintBanner();

// ─── CancellationToken for Ctrl+C ───────────────────────

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Log.Warn("收到 Ctrl+C，正在优雅关闭...");
};

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    cts.Cancel();
};

// ─── Connect with Retry ─────────────────────────────────

await using var client = new GatewayClient(options);

client.OnEvent("shutdown", _ =>
{
    Log.Warn("Gateway 正在关闭，准备重连...");
    return Task.CompletedTask;
});

client.Events.On("*", evt =>
{
    var ignored = new HashSet<string>
    {
        "connect.challenge", "tick", "heartbeat", "health",
        "presence", "shutdown",
    };
    if (!ignored.Contains(evt.Event))
        Log.Event(evt.Event);
    return Task.CompletedTask;
});

Log.Info($"目标: {gatewayUrl}");
Log.Info("Connecting...");

try
{
    await client.ConnectAsync(cts.Token);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("NOT_PAIRED", StringComparison.OrdinalIgnoreCase))
{
    Log.Error("当前设备未被批准，请先在 Gateway 控制面板中手动批准此设备");
    return 1;
}
catch (Exception ex)
{
    Log.Error($"连接失败: {ex.Message}");
    return 1;
}

Log.Success("Connected!");

// ─── Start Auto-Approve Service ─────────────────────────

Log.Info("Polling pairing requests...");

var service = new AutoApproveService(client, TimeSpan.FromSeconds(pollIntervalSec));

try
{
    await service.StartAsync(cts.Token);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("NOT_PAIRED"))
{
    Log.Error("请先在 Gateway 控制面板中手动批准此服务自身，然后重新启动");
    return 1;
}
catch (OperationCanceledException)
{
    // Ctrl+C graceful exit
}
catch (Exception ex)
{
    Log.Error($"服务异常退出: {ex.Message}");
    return 1;
}

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("  Auto-approve service stopped. Goodbye!");
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
    Console.Write(" Auto-Approve Service");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  v1.0.0");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  ─────────────────────────────────────────────────");
    Console.ResetColor();
    Console.WriteLine();
}

sealed record AppConfig
{
    public string GatewayUrl { get; init; } = GatewayConstants.DefaultGatewayUrl;
    public string GatewayToken { get; init; } = "YOUR_GATEWAY_TOKEN";
    public double PollIntervalSeconds { get; init; } = 2;
}
