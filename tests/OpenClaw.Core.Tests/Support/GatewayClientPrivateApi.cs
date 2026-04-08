using System.Reflection;
using System.Text.Json;
using OpenClaw.Core.Client;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Tests.Support;

/// <summary>
/// 通过反射调用 <see cref="GatewayClient"/> 的私有/静态辅助逻辑，用于提高分支覆盖率而不污染生产 API。
/// </summary>
internal static class GatewayClientPrivateApi
{
    private static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;

    /// <summary>
    /// 反射调用私有实例方法 <c>ProcessHelloOk</c>，将握手结果写入客户端缓存状态。
    /// </summary>
    public static void ProcessHelloOk(GatewayClient client, GatewayResponse response)
    {
        var mi = typeof(GatewayClient).GetMethod("ProcessHelloOk", Instance)
                 ?? throw new InvalidOperationException("ProcessHelloOk not found");
        mi.Invoke(client, [response]);
    }

    /// <summary>
    /// 反射调用私有实例方法 <c>CalculateBackoff</c>，验证指数退避公式。
    /// </summary>
    public static TimeSpan CalculateBackoff(GatewayClient client, int attempt)
    {
        var mi = typeof(GatewayClient).GetMethod("CalculateBackoff", Instance)
                 ?? throw new InvalidOperationException("CalculateBackoff not found");
        return (TimeSpan)mi.Invoke(client, [attempt])!;
    }

    /// <summary>
    /// 反射调用私有实例方法 <c>BuildConnectParams</c>，验证连接参数拼装（含签名字段）。
    /// </summary>
    public static ConnectParams BuildConnectParams(GatewayClient client, string nonce, bool deviceTokenOnly = false)
    {
        var mi = typeof(GatewayClient).GetMethod("BuildConnectParams", Instance)
                 ?? throw new InvalidOperationException("BuildConnectParams not found");
        return (ConnectParams)mi.Invoke(client, [nonce, deviceTokenOnly])!;
    }

    /// <summary>
    /// 反射调用静态方法 <c>IsNotPairedError</c>。
    /// </summary>
    public static bool IsNotPairedError(JsonElement? error)
    {
        var mi = typeof(GatewayClient).GetMethod("IsNotPairedError", Static)
                 ?? throw new InvalidOperationException("IsNotPairedError not found");
        return (bool)mi.Invoke(null, [error])!;
    }

    /// <summary>
    /// 反射调用静态方法 <c>TryParseAuthError</c>，解析认证/设备认证错误详情。
    /// </summary>
    public static AuthErrorDetails? TryParseAuthError(JsonElement? error)
    {
        var mi = typeof(GatewayClient).GetMethod("TryParseAuthError", Static)
                 ?? throw new InvalidOperationException("TryParseAuthError not found");
        return (AuthErrorDetails?)mi.Invoke(null, [error]);
    }

    /// <summary>
    /// 反射调用静态方法 <c>GetString</c>（从可空 JsonElement 取属性字符串）。
    /// </summary>
    public static string GetString(JsonElement? element, string prop)
    {
        var mi = typeof(GatewayClient).GetMethod("GetString", Static)
                 ?? throw new InvalidOperationException("GetString not found");
        return (string)mi.Invoke(null, [element, prop])!;
    }

    /// <summary>
    /// 将 <see cref="HelloOkPayload"/> 序列化后封装为成功的 <see cref="GatewayResponse"/>，便于喂给 <see cref="ProcessHelloOk"/>。
    /// </summary>
    public static GatewayResponse ResponseWithHelloOk(HelloOkPayload payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonDefaults.SerializerOptions);
        var el = JsonDocument.Parse(json).RootElement.Clone();
        return new GatewayResponse { Ok = true, Id = "", Payload = el };
    }
}
