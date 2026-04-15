using System.Text.Json;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Models;
using Xunit;

namespace OpenClaw.Core.Tests;

public sealed class GatewayOutboundMessageSanitizerTests
{
    [Fact]
    public void SanitizeOutboundUserText_strips_invisible_and_directives()
    {
        var raw = "hi\u200B<directive>x</directive>";
        var s = GatewayOutboundMessageSanitizer.SanitizeOutboundUserText(raw);
        Assert.Equal("hi", s);
    }

    [Fact]
    public void SanitizeRpcParams_chat_send_message()
    {
        using var doc = JsonDocument.Parse("""{"sessionKey":"main","message":"a<tool_call>x</tool_call>b"}""");
        var next = GatewayOutboundMessageSanitizer.SanitizeRpcParams(GatewayConstants.Methods.ChatSend, doc.RootElement);
        Assert.Equal("ab", next.GetProperty("message").GetString());
    }

    [Fact]
    public void ResolveSanitizedField_known_methods()
    {
        Assert.Equal("message", GatewayOutboundMessageSanitizer.ResolveSanitizedField(GatewayConstants.Methods.ChatSend));
        Assert.Equal("content", GatewayOutboundMessageSanitizer.ResolveSanitizedField(GatewayConstants.Methods.ChatInject));
        Assert.Equal("text", GatewayOutboundMessageSanitizer.ResolveSanitizedField(GatewayConstants.Methods.Send));
        Assert.Null(GatewayOutboundMessageSanitizer.ResolveSanitizedField("health"));
    }
}
