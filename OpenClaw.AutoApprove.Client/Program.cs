using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenClaw.AutoApprove.Core;
using OpenClaw.AutoApprove.Core.Extensions;
using OpenClaw.Core.Extensions;
using OpenClaw.Core.Logging;
using OpenClaw.Core.Models;

PrintBanner();

var builder = Host.CreateApplicationBuilder(args);

// ─── State Directory ────────────────────────────────────

var stateDir = Environment.GetEnvironmentVariable("OPENCLAW_STATE_DIR")
               ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw-autoapprove");

// ─── Register OpenClaw Core Services ────────────────────

builder.Services.AddOpenClaw(builder.Configuration.GetSection(GatewayOptions.SectionName));

builder.Services.PostConfigure<GatewayOptions>(opts =>
{
    opts.KeyFilePath ??= Path.Combine(stateDir, GatewayConstants.FileNames.DeviceKey);
    opts.DeviceTokenFilePath ??= Path.Combine(stateDir, GatewayConstants.FileNames.DeviceToken);
});

// ─── Register AutoApprove Service ───────────────────────

builder.Services.AddAutoApprove(builder.Configuration.GetSection(AutoApproveOptions.SectionName));

// ─── Build & Run ────────────────────────────────────────

using var host = builder.Build();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Log.Warn("收到 Ctrl+C，正在优雅关闭...");
};

try
{
    await host.RunAsync();
}
catch (OperationCanceledException)
{
    // graceful shutdown
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
