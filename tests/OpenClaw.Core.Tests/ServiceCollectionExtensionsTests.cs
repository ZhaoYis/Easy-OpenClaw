using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Core.Client;
using OpenClaw.Core.Extensions;
using OpenClaw.Core.Models;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="ServiceCollectionExtensions"/> 的 DI 注册与校验测试。
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// <see cref="ServiceCollectionExtensions.AddOpenClaw(IServiceCollection, Action{GatewayOptions})"/> 应注册可解析的单例。
    /// </summary>
    [Fact]
    public void AddOpenClaw_with_delegate_registers_singletons()
    {
        var services = new ServiceCollection();
        services.AddOpenClaw(o =>
        {
            o.Url = "ws://localhost:1";
            o.Token = "secret";
            o.KeyFilePath = null;
        });
        var sp = services.BuildServiceProvider();
        _ = sp.GetRequiredService<GatewayClient>();
        _ = sp.GetRequiredService<EventRouter>();
        _ = sp.GetRequiredService<GatewayRequestManager>();
        _ = sp.GetRequiredService<DeviceIdentity>();
    }

    /// <summary>
    /// <see cref="ServiceCollectionExtensions.AddOpenClaw(IServiceCollection, IConfigurationSection)"/> 应从配置绑定并校验 Url/Token。
    /// </summary>
    [Fact]
    public void AddOpenClaw_with_configuration_section_binds_options()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{GatewayOptions.SectionName}:Url"] = "ws://localhost:2",
            [$"{GatewayOptions.SectionName}:Token"] = "tok",
        }).Build();
        var services = new ServiceCollection();
        services.AddOpenClaw(cfg.GetSection(GatewayOptions.SectionName));
        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GatewayOptions>>().Value;
        Assert.Equal("ws://localhost:2", opts.Url);
        Assert.Equal("tok", opts.Token);
    }

    /// <summary>
    /// <see cref="ServiceCollectionExtensions.UseOpenClawEventSubscriber"/> 应注册 <see cref="GatewayEventSubscriber"/>。
    /// </summary>
    [Fact]
    public void UseOpenClawEventSubscriber_registers_subscriber()
    {
        var services = new ServiceCollection();
        services.AddOpenClaw(o =>
        {
            o.Url = "ws://x";
            o.Token = "t";
            o.KeyFilePath = null;
        });
        services.UseOpenClawEventSubscriber();
        var sp = services.BuildServiceProvider();
        _ = sp.GetRequiredService<GatewayEventSubscriber>();
    }
}
