using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Core.SignalR;

namespace OpenClaw.Tests.SignalR;

/// <summary>集成测试用：验证 <see cref="OpenClawSignalROperationControllerBase{THub}"/> 路由与绑定。</summary>
[ApiController]
[AllowAnonymous]
[Route("api/test/openclaw/signalr/operations")]
public sealed class TestOpenClawOpsController : OpenClawSignalROperationControllerBase<OpenClawGatewayHub>
{
    public TestOpenClawOpsController(IOpenClawSignalROperationService<OpenClawGatewayHub> operations)
        : base(operations)
    {
    }
}
