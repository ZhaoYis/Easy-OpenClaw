using OpenClaw.Core.Models;

namespace OpenClaw.Core.Client;

/// <summary>
/// 网关客户端在握手前后需要持久化或按用户隔离的状态（DeviceToken、已授予的 scopes 等）。
/// 默认实现为基于 <see cref="GatewayOptions.DeviceTokenFilePath"/> /
/// <see cref="GatewayOptions.DeviceScopesFilePath"/> 的文件存储；应用可为每个用户构造不同的实现并配合多个 <see cref="GatewayClient"/> 实例使用。
/// </summary>
public interface IGatewayClientStateStore
{
    /// <summary>构造客户端时调用，加载已缓存的 DeviceToken（无则返回 null）。</summary>
    string? LoadDeviceToken();

    /// <summary>在握手获得新 DeviceToken 或 bootstrap handoff 时调用。</summary>
    void SaveDeviceToken(string token);

    /// <summary>构造客户端时调用，加载已缓存的 scopes（无则返回 null）。</summary>
    string[]? LoadDeviceScopes();

    /// <summary>在握手获得服务端授予的 scopes 时调用。</summary>
    void SaveDeviceScopes(string[] scopes);
}
