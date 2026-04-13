using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 将 <see cref="IOpenClawSignalROperationService{THub}"/> 暴露为 REST 端点的抽象控制器基类。
/// 派生类须自行配置 <c>[Route(...)]</c> 与生产环境鉴权（例如 <c>[Authorize]</c>）；本基类不施加授权策略。
/// </summary>
/// <remarks>
/// 与 SignalR 同进程共用时，建议在 <c>Program.cs</c> 中先调用 <c>MapControllers()</c> 再 <c>MapHub&lt;THub&gt;(...)</c>，
/// 以免端点匹配顺序导致 Hub 断开时运营快照未正确清理。
/// </remarks>
/// <typeparam name="THub">与 <c>MapHub&lt;THub&gt;</c> 及 DI 中注册的 Hub 类型一致。</typeparam>
[ApiController]
public abstract class OpenClawSignalROperationControllerBase<THub> : ControllerBase
    where THub : Hub
{
    private readonly IOpenClawSignalROperationService<THub> _operations;

    protected OpenClawSignalROperationControllerBase(IOpenClawSignalROperationService<THub> operations)
    {
        _operations = operations;
    }

    /// <summary>当前在线连接快照列表。</summary>
    [HttpGet("connections")]
    [ProducesResponseType(typeof(IReadOnlyList<OpenClawSignalRConnectionSnapshot>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OpenClawSignalRConnectionSnapshot>>> GetConnections(
        CancellationToken cancellationToken) =>
        Ok(await _operations.GetOnlineConnectionsAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>当前在线连接数。</summary>
    [HttpGet("connections/count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetConnectionCount(CancellationToken cancellationToken) =>
        Ok(await _operations.GetOnlineConnectionCountAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>已认证连接的去重用户 id。</summary>
    [HttpGet("users/distinct")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetDistinctUserIds(CancellationToken cancellationToken) =>
        Ok(await _operations.GetDistinctOnlineUserIdsAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>指定用户下的连接快照。</summary>
    [HttpGet("users/{userId}/connections")]
    [ProducesResponseType(typeof(IReadOnlyList<OpenClawSignalRConnectionSnapshot>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OpenClawSignalRConnectionSnapshot>>> GetConnectionsForUser(
        string userId,
        CancellationToken cancellationToken) =>
        Ok(await _operations.GetConnectionsForUserAsync(userId, cancellationToken).ConfigureAwait(false));

    /// <summary>各 SignalR 组当前覆盖的连接数。</summary>
    [HttpGet("groups/counts")]
    [ProducesResponseType(typeof(IReadOnlyDictionary<string, int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyDictionary<string, int>>> GetGroupConnectionCounts(
        CancellationToken cancellationToken) =>
        Ok(await _operations.GetSignalRGroupConnectionCountsAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>与 Hub 单用户组名一致的格式化组名。</summary>
    [HttpGet("groups/formatted/user/{userId}")]
    [ProducesResponseType(typeof(OpenClawSignalRFormattedGroupNameResponse), StatusCodes.Status200OK)]
    public ActionResult<OpenClawSignalRFormattedGroupNameResponse> GetFormattedUserGroupName(string userId) =>
        Ok(new OpenClawSignalRFormattedGroupNameResponse(_operations.FormatUserGroupName(userId)));

    /// <summary>与 Hub 档位组名一致的格式化组名。</summary>
    [HttpGet("groups/formatted/tier/{tier}")]
    [ProducesResponseType(typeof(OpenClawSignalRFormattedGroupNameResponse), StatusCodes.Status200OK)]
    public ActionResult<OpenClawSignalRFormattedGroupNameResponse> GetFormattedTierGroupName(string tier) =>
        Ok(new OpenClawSignalRFormattedGroupNameResponse(_operations.FormatTierGroupName(tier)));

    /// <summary>向该用户的所有连接调用客户端 Hub 方法。</summary>
    [HttpPost("send/user")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SendToUserAsync(
        [FromBody] OpenClawSignalRSendToUserRequest request,
        CancellationToken cancellationToken)
    {
        await _operations.SendToUserAsync(
                request.UserId,
                request.HubMethod,
                ToObjectArgs(request.Args),
                cancellationToken)
            .ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>向指定连接调用客户端 Hub 方法。</summary>
    [HttpPost("send/connection")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SendToConnectionAsync(
        [FromBody] OpenClawSignalRSendToConnectionRequest request,
        CancellationToken cancellationToken)
    {
        await _operations.SendToConnectionAsync(
                request.ConnectionId,
                request.HubMethod,
                ToObjectArgs(request.Args),
                cancellationToken)
            .ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>向指定组调用客户端 Hub 方法。</summary>
    [HttpPost("send/group")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SendToGroupAsync(
        [FromBody] OpenClawSignalRSendToGroupRequest request,
        CancellationToken cancellationToken)
    {
        await _operations.SendToGroupAsync(
                request.GroupName,
                request.HubMethod,
                ToObjectArgs(request.Args),
                cancellationToken)
            .ConfigureAwait(false);
        return NoContent();
    }

    private static object?[]? ToObjectArgs(JsonElement[]? args)
    {
        if (args is null || args.Length == 0)
            return null;

        var result = new object?[args.Length];
        for (var i = 0; i < args.Length; i++)
            result[i] = args[i].Deserialize<object?>();

        return result;
    }
}