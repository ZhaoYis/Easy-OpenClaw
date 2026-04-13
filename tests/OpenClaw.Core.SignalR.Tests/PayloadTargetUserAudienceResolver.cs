using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using OpenClaw.Core.SignalR;

namespace OpenClaw.Tests.SignalR;

/// <summary>测试用：从事件 <c>Payload.targetUserId</c> 解析推送目标组（与 Hub 一致：<see cref="OpenClawSignalRGroupNames.FormatUserGroup"/>）；生产环境优先使用基于建连分组的解析器。</summary>
public sealed class PayloadTargetUserAudienceResolver : IGatewayEventAudienceResolver
{
    private readonly IOptions<OpenClawSignalROptions> _options;

    public PayloadTargetUserAudienceResolver(IOptions<OpenClawSignalROptions> options)
    {
        _options = options;
    }

    public bool TryResolveClients(GatewayEventAudienceResolveContext context, [NotNullWhen(true)] out IClientProxy? target)
    {
        target = null;
        if (context.Event.Payload is not { } p || !p.TryGetProperty("targetUserId", out var idEl))
            return false;

        var uid = idEl.GetString();
        if (string.IsNullOrEmpty(uid))
            return false;

        var group = OpenClawSignalRGroupNames.FormatUserGroup(_options.Value, uid);
        target = context.Clients.Group(group);
        return true;
    }
}
