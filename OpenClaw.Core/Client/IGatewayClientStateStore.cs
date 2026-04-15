using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 网关客户端在握手前后需要持久化或按用户隔离的状态（DeviceToken、已授予的 scopes 等）。
/// 默认实现为基于 <see cref="GatewayOptions.DeviceTokenFilePath"/> /
/// <see cref="GatewayOptions.DeviceScopesFilePath"/> 的文件存储；应用可为每个用户构造不同的实现并配合多个 <see cref="GatewayClient"/> 实例使用。
/// 自定义实现可在各方法的 <c>state</c> 与 <see cref="GatewayOptions"/> 中区分用户或租户，实现分用户持久化；
/// 参数顺序与 <see cref="IGatewayClientConnectionResolver"/> 一致（<c>state</c>、配置、取消）。
/// </summary>
public interface IGatewayClientStateStore
{
    /// <summary>在需要用到持久化 DeviceToken 时调用（如每次建连开始前），无缓存则返回 null。</summary>
    /// <param name="state">调用方传入的任意上下文（如用户 id）；未建连前多为 null。</param>
    /// <param name="gatewayOptions">当前客户端使用的网关配置（路径、超时等）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<string?> LoadDeviceTokenAsync(object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default);

    /// <summary>在握手获得新 DeviceToken 或 bootstrap handoff 时调用。</summary>
    /// <param name="state">与本次建连传入的上下文一致，便于按用户落盘。</param>
    /// <param name="gatewayOptions">当前客户端使用的网关配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SaveDeviceTokenAsync(string token, object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default);

    /// <summary>在需要用到持久化 scopes 时调用（如每次建连开始前），无缓存则返回 null。</summary>
    /// <param name="state">调用方传入的任意上下文（如用户 id）；未建连前多为 null。</param>
    /// <param name="gatewayOptions">当前客户端使用的网关配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<string[]?> LoadDeviceScopesAsync(object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default);

    /// <summary>在握手获得服务端授予的 scopes 时调用。</summary>
    /// <param name="state">与本次建连传入的上下文一致，便于按用户落盘。</param>
    /// <param name="gatewayOptions">当前客户端使用的网关配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SaveDeviceScopesAsync(string[] scopes, object? state, GatewayOptions gatewayOptions, CancellationToken cancellationToken = default);
}