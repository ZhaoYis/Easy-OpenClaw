using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 网关客户端在握手前后需要持久化或按调用方上下文隔离的状态：主要是 DeviceToken 与网关授予的 scopes。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GatewayClient"/> 在每次建连开始时（<see cref="GatewayClient.ConnectAsync(System.Threading.CancellationToken)"/> /
/// <see cref="GatewayClient.ConnectAsync(object?, System.Threading.CancellationToken)"/>）会依次调用
/// <see cref="LoadDeviceTokenAsync"/> 与 <see cref="LoadDeviceScopesAsync"/>，将结果用于 connect 签名与 auth 载荷；
/// 传入的 <c>state</c> 与 <see cref="IGatewayClientConnectionResolver"/> 一致，便于多用户场景下与 URL/指纹解析对齐。
/// </para>
/// <para>
/// 收到 hello-ok 且 payload 中含 <c>auth.deviceToken</c> / <c>auth.scopes</c> 时，客户端会更新内存并调用
/// <see cref="SaveDeviceTokenAsync"/> / <see cref="SaveDeviceScopesAsync"/>；
/// 在受信任的传输上处理 bootstrap handoff（<c>auth.deviceTokens</c>）时也会再次写入主 token 与 scopes。
/// </para>
/// <para>
/// 默认实现为 <see cref="FileGatewayClientStateStore"/>，路径来自
/// <see cref="GatewayOptions.DeviceTokenFilePath"/> 与 <see cref="GatewayOptions.DeviceScopesFilePath"/>，并忽略 <c>state</c>。
/// 自定义实现应使用 <c>state</c> 与/或 <see cref="GatewayOptions"/> 区分租户或用户，使多个 <see cref="GatewayClient"/> 实例各自持久化独立数据。
/// </para>
/// </remarks>
public interface IGatewayClientStateStore
{
    /// <summary>
    /// 读取此前保存的 DeviceToken，供建连前的认证与签名使用。
    /// </summary>
    /// <param name="state">与本次 <see cref="GatewayClient.ConnectAsync(object?, System.Threading.CancellationToken)"/> 传入的上下文相同；单用户场景多为 <c>null</c>。</param>
    /// <param name="gatewayOptions">当前客户端绑定的网关选项（路径、超时等），实现可从中解析存储位置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已保存的令牌；不存在、为空或实现选择忽略错误时返回 <c>null</c>。</returns>
    Task<string?> LoadDeviceTokenAsync(object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 持久化新的 DeviceToken；在握手返回新 token 或 bootstrap handoff 选定主 token 时由客户端调用。
    /// </summary>
    /// <param name="token">要保存的 DeviceToken 明文。</param>
    /// <param name="state">与本次建连传入的上下文一致，便于按用户或租户落盘。</param>
    /// <param name="gatewayOptions">当前客户端绑定的网关选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示写入操作完成的任务；若无法持久化，实现可吞掉异常并完成任务（见 <see cref="FileGatewayClientStateStore"/>），或由实现方约定是否抛出。</returns>
    Task SaveDeviceTokenAsync(string token, object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取此前保存的网关授予 scopes，供建连时与签名、授权声明一致。
    /// </summary>
    /// <param name="state">与本次 <see cref="GatewayClient.ConnectAsync(object?, System.Threading.CancellationToken)"/> 传入的上下文相同。</param>
    /// <param name="gatewayOptions">当前客户端绑定的网关选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已保存的作用域列表；不存在、反序列化失败或实现选择忽略错误时返回 <c>null</c>。</returns>
    Task<string[]?> LoadDeviceScopesAsync(object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 持久化网关在本次握手中授予的 scopes，供下次建连复用。
    /// </summary>
    /// <param name="scopes">要保存的作用域标识数组（顺序与内容宜与网关 <c>auth.scopes</c> 一致）。</param>
    /// <param name="state">与本次建连传入的上下文一致。</param>
    /// <param name="gatewayOptions">当前客户端绑定的网关选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示写入操作完成的任务；持久化失败时的行为由实现决定（参考 <see cref="FileGatewayClientStateStore"/> 仅记录警告）。</returns>
    Task SaveDeviceScopesAsync(string[] scopes, object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default);
}
