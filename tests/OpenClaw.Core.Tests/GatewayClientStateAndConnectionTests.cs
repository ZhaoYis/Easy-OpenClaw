using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenClaw.Core.Client;
using OpenClaw.Core.Extensions;
using OpenClaw.Core.Models;
using OpenClaw.Core.Tests.Support;
using OpenClaw.Core.Transport;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="IGatewayClientStateStore"/>、<see cref="FileGatewayClientStateStore"/> 与
/// <see cref="IGatewayClientConnectionResolver"/> 对 <see cref="GatewayClient"/> 的集成行为测试。
/// </summary>
public sealed class GatewayClientStateAndConnectionTests
{
    private sealed class RecordingStateStore : IGatewayClientStateStore
    {
        public string? InitialToken { get; init; }
        public string[]? InitialScopes { get; init; }
        public string? LastSavedToken { get; private set; }
        public string[]? LastSavedScopes { get; private set; }
        public int SaveTokenCount { get; private set; }
        public int SaveScopesCount { get; private set; }

        public object? LastLoadedState { get; private set; }
        public object? LastSavedState { get; private set; }
        public GatewayOptions? LastGatewayOptions { get; private set; }

        public Task<string?> LoadDeviceTokenAsync(object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default)
        {
            LastLoadedState = state;
            LastGatewayOptions = gatewayOptions;
            return Task.FromResult(InitialToken);
        }

        public Task SaveDeviceTokenAsync(string token, object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default)
        {
            SaveTokenCount++;
            LastSavedToken = token;
            LastSavedState = state;
            LastGatewayOptions = gatewayOptions;
            return Task.CompletedTask;
        }

        public Task<string[]?> LoadDeviceScopesAsync(object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default)
        {
            LastLoadedState = state;
            LastGatewayOptions = gatewayOptions;
            return Task.FromResult(InitialScopes);
        }

        public Task SaveDeviceScopesAsync(string[] scopes, object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default)
        {
            SaveScopesCount++;
            LastSavedScopes = scopes;
            LastSavedState = state;
            LastGatewayOptions = gatewayOptions;
            return Task.CompletedTask;
        }
    }

    private sealed class CustomConnectionResolver(string? url, string? tls) : IGatewayClientConnectionResolver
    {
        public ValueTask<string> ResolveWebSocketUrlAsync(object? state, GatewayOptions gatewayOptions, CancellationToken ct) =>
            ValueTask.FromResult(url ?? string.Empty);

        public ValueTask<string?> ResolveTlsFingerprintAsync(object? state, GatewayOptions gatewayOptions, CancellationToken ct) =>
            ValueTask.FromResult(tls);
    }

    /// <summary>
    /// <see cref="FileGatewayClientStateStore"/> 应从配置路径读写 token，且路径为 null 时加载为 null。
    /// </summary>
    [Fact]
    public async Task FileStateStore_roundtrips_token_when_path_configured()
    {
        var path = Path.Combine(Path.GetTempPath(), $"openclaw-dt-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, "  tok-from-disk  \n");
            var gatewayOptions = new GatewayOptions
            {
                Url = "ws://x",
                Token = "t",
                DeviceTokenFilePath = path,
                DeviceScopesFilePath = null,
            };
            var store = new FileGatewayClientStateStore();
            Assert.Equal("tok-from-disk", await store.LoadDeviceTokenAsync(null, gatewayOptions));
            await store.SaveDeviceTokenAsync("new-tok", null, gatewayOptions);
            Assert.Equal("new-tok", File.ReadAllText(path).Trim());
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // best-effort
            }
        }
    }

    /// <summary>
    /// <see cref="FileGatewayClientStateStore"/> 应读写 scopes JSON 数组。
    /// </summary>
    [Fact]
    public async Task FileStateStore_roundtrips_scopes_json()
    {
        var path = Path.Combine(Path.GetTempPath(), $"openclaw-sc-{Guid.NewGuid():N}.json");
        try
        {
            var gatewayOptions = new GatewayOptions
            {
                Url = "ws://x",
                Token = "t",
                DeviceTokenFilePath = null,
                DeviceScopesFilePath = path,
            };
            var store = new FileGatewayClientStateStore();
            Assert.Null(await store.LoadDeviceScopesAsync(null, gatewayOptions));
            await store.SaveDeviceScopesAsync(["a", "b"], null, gatewayOptions);
            var loaded = await store.LoadDeviceScopesAsync(null, gatewayOptions);
            Assert.NotNull(loaded);
            Assert.Equal(new[] { "a", "b" }, loaded);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // best-effort
            }
        }
    }

    /// <summary>
    /// 自定义 <see cref="IGatewayClientStateStore"/> 应在拉取持久化状态后反映 Initial token/scopes，并在 ProcessHelloOk 时触发保存。
    /// </summary>
    [Fact]
    public async Task Custom_state_store_loads_initial_values_and_receives_saves_on_hello_ok()
    {
        var state = new RecordingStateStore
        {
            InitialToken = "preloaded",
            InitialScopes = ["scope.x"],
        };
        var opts = Options.Create(new GatewayOptions { Url = "ws://unit.test", Token = "test-token", KeyFilePath = null });
        var requests = new GatewayRequestManager(opts);
        var events = new EventRouter();
        var device = DeviceIdentity.LoadOrCreate(null);
        var client = new GatewayClient(
            opts,
            requests,
            events,
            device,
            state,
            new DefaultGatewayClientConnectionResolver());
        client.AttachWebSocketForTests(new LoopbackWebSocket(client));

        await GatewayClientPrivateApi.LoadPersistedDeviceStateAsync(client);
        var p = GatewayClientPrivateApi.BuildConnectParams(client, "n");
        Assert.Equal("preloaded", p.Auth?.DeviceToken);

        var hello = new HelloOkPayload
        {
            Type = "hello-ok",
            Protocol = 1,
            Features = new FeaturesInfo { Methods = ["health"], Events = [] },
            Auth = new HelloAuthInfo
            {
                DeviceToken = "fresh-token",
                Scopes = ["s1", "s2"],
            },
        };
        GatewayClientPrivateApi.ProcessHelloOk(client, GatewayClientPrivateApi.ResponseWithHelloOk(hello));

        Assert.Equal(1, state.SaveTokenCount);
        Assert.Equal("fresh-token", state.LastSavedToken);
        Assert.Equal(1, state.SaveScopesCount);
        Assert.Equal(new[] { "s1", "s2" }, state.LastSavedScopes);
        Assert.Same(opts.Value, state.LastGatewayOptions);
    }

    /// <summary>
    /// 两个客户端使用不同 <see cref="IGatewayClientStateStore"/> 时应各自维护独立的 connect 签名上下文（deviceToken + scopes）。
    /// </summary>
    [Fact]
    public async Task Two_clients_isolated_state_produce_distinct_connect_auth()
    {
        var baseOpts = new GatewayOptions { Url = "ws://unit.test", Token = "shared", KeyFilePath = null };
        var opts = Options.Create(baseOpts);

        static GatewayClient CreateClient(IOptions<GatewayOptions> o, IGatewayClientStateStore store)
        {
            var requests = new GatewayRequestManager(o);
            var events = new EventRouter();
            var device = DeviceIdentity.LoadOrCreate(null);
            return new GatewayClient(o, requests, events, device, store, new DefaultGatewayClientConnectionResolver());
        }

        var a = CreateClient(opts, new RecordingStateStore { InitialToken = "tok-a", InitialScopes = ["a"] });
        var b = CreateClient(opts, new RecordingStateStore { InitialToken = "tok-b", InitialScopes = ["b"] });

        await GatewayClientPrivateApi.LoadPersistedDeviceStateAsync(a);
        await GatewayClientPrivateApi.LoadPersistedDeviceStateAsync(b);
        var pa = GatewayClientPrivateApi.BuildConnectParams(a, "nonce");
        var pb = GatewayClientPrivateApi.BuildConnectParams(b, "nonce");
        Assert.Equal("tok-a", pa.Auth?.DeviceToken);
        Assert.Equal("tok-b", pb.Auth?.DeviceToken);
        Assert.Equal(new[] { "a" }, pa.Scopes);
        Assert.Equal(new[] { "b" }, pb.Scopes);
    }

    /// <summary>
    /// <see cref="DefaultGatewayClientConnectionResolver"/> 下有效 URL 与指纹应等于 <see cref="GatewayOptions"/>。
    /// </summary>
    [Fact]
    public async Task Default_connection_overrides_fallback_to_gateway_options()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(
            c => new LoopbackWebSocket(c),
            o =>
            {
                o.Url = "wss://gateway.example/ws";
                o.TlsFingerprint = "aa:bb";
            });
        Assert.Equal("wss://gateway.example/ws", await client.GetEffectiveWebSocketUrlAsync(null, CancellationToken.None));
        Assert.Equal("aa:bb", await client.GetEffectiveTlsFingerprintAsync(null, CancellationToken.None));
    }

    /// <summary>
    /// 连接覆盖返回非空时应取代配置中的默认 Url / TlsFingerprint。
    /// </summary>
    [Fact]
    public async Task Custom_connection_overrides_replace_options_when_non_blank()
    {
        var opts = Options.Create(new GatewayOptions
        {
            Url = "ws://default",
            Token = "t",
            TlsFingerprint = "default-fp",
            KeyFilePath = null,
        });
        var requests = new GatewayRequestManager(opts);
        var events = new EventRouter();
        var device = DeviceIdentity.LoadOrCreate(null);
        var state = new RecordingStateStore();
        var client = new GatewayClient(
            opts,
            requests,
            events,
            device,
            state,
            new CustomConnectionResolver("ws://per-user", "user-fp"));

        Assert.Equal("ws://per-user", await client.GetEffectiveWebSocketUrlAsync(null, CancellationToken.None));
        Assert.Equal("user-fp", await client.GetEffectiveTlsFingerprintAsync(null, CancellationToken.None));
    }

    /// <summary>
    /// 覆盖值为仅空白字符串时应回退到 <see cref="GatewayOptions"/>。
    /// </summary>
    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public async Task Blank_connection_override_falls_back_to_options(string blank)
    {
        var opts = Options.Create(new GatewayOptions
        {
            Url = "ws://fallback",
            Token = "t",
            TlsFingerprint = "fp-fallback",
            KeyFilePath = null,
        });
        var client = new GatewayClient(
            opts,
            new GatewayRequestManager(opts),
            new EventRouter(),
            DeviceIdentity.LoadOrCreate(null),
            new RecordingStateStore(),
            new CustomConnectionResolver(blank, blank));

        Assert.Equal("ws://fallback", await client.GetEffectiveWebSocketUrlAsync(null, CancellationToken.None));
        Assert.Equal("fp-fallback", await client.GetEffectiveTlsFingerprintAsync(null, CancellationToken.None));
    }

    /// <summary>
    /// <see cref="ServiceCollectionExtensions.AddOpenClaw"/> 应注册默认可解析的 state store 与 connection overrides。
    /// </summary>
    [Fact]
    public void AddOpenClaw_registers_state_store_and_connection_overrides()
    {
        var services = new ServiceCollection();
        services.AddOpenClaw(o =>
        {
            o.Url = "ws://localhost:1";
            o.Token = "secret";
            o.KeyFilePath = null;
        });
        var sp = services.BuildServiceProvider();
        _ = sp.GetRequiredService<IGatewayClientStateStore>();
        _ = sp.GetRequiredService<IGatewayClientConnectionResolver>();
        Assert.IsType<FileGatewayClientStateStore>(sp.GetRequiredService<IGatewayClientStateStore>());
        Assert.IsType<DefaultGatewayClientConnectionResolver>(sp.GetRequiredService<IGatewayClientConnectionResolver>());
    }
}
