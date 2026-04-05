using System.Text.Json;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Models;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="JsonDefaults"/> 与网关 DTO（<see cref="GatewayRequest"/> / <see cref="GatewayResponse"/> / <see cref="GatewayEvent"/>）的序列化冒烟测试。
/// </summary>
public sealed class JsonAndGatewayMessagesTests
{
    /// <summary>
    /// <see cref="JsonDefaults.SerializerOptions"/> 应启用 camelCase 与按 null 忽略写入。
    /// </summary>
    [Fact]
    public void SerializerOptions_uses_camel_case_and_ignores_null_on_write()
    {
        var json = JsonSerializer.Serialize(new { FooBar = 1, Baz = (string?)null }, JsonDefaults.SerializerOptions);
        Assert.Contains("fooBar", json);
        Assert.DoesNotContain("baz", json);
    }

    /// <summary>
    /// <see cref="GatewayRequest"/> 应序列化为带 type/id/method/params 的 JSON。
    /// </summary>
    [Fact]
    public void GatewayRequest_round_trips()
    {
        var req = new GatewayRequest
        {
            Id = "id-1",
            Method = "health",
            Params = JsonSerializer.SerializeToElement(new { a = 1 }, JsonDefaults.SerializerOptions),
        };
        var json = JsonSerializer.Serialize(req, JsonDefaults.SerializerOptions);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(GatewayConstants.FrameTypes.Request, doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("health", doc.RootElement.GetProperty("method").GetString());
    }

    /// <summary>
    /// <see cref="RawFrame"/> 应能承载响应与事件两种形态的反序列化字段。
    /// </summary>
    [Fact]
    public void RawFrame_deserializes_response_and_event_shapes()
    {
        var res = JsonSerializer.Deserialize<RawFrame>(
            """{"type":"res","id":"x","ok":true,"payload":{}}""",
            JsonDefaults.SerializerOptions);
        Assert.Equal("res", res?.Type);
        var ev = JsonSerializer.Deserialize<RawFrame>(
            """{"type":"event","event":"tick","seq":1}""",
            JsonDefaults.SerializerOptions);
        Assert.Equal("tick", ev?.Event);
    }
}
