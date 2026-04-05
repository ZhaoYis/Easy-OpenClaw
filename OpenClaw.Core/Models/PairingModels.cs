using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// device.pair.list 接口的响应体，按状态分组返回所有设备配对请求。
/// </summary>
public sealed record PairListResponse
{
    /// <summary>待审批的配对请求列表</summary>
    [JsonPropertyName("pending")]
    public PairRequest[] Pending { get; init; } = [];

    /// <summary>已批准的配对请求列表</summary>
    [JsonPropertyName("approved")]
    public PairRequest[] Approved { get; init; } = [];

    /// <summary>已拒绝的配对请求列表</summary>
    [JsonPropertyName("rejected")]
    public PairRequest[] Rejected { get; init; } = [];
}

/// <summary>
/// 单个设备配对请求的详细信息。
/// </summary>
public sealed record PairRequest
{
    /// <summary>配对请求的唯一标识符</summary>
    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = "";

    /// <summary>发起配对的设备 ID（SHA-256 公钥哈希）</summary>
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; init; } = "";

    /// <summary>发起配对的客户端标识（如 "cli"、"webchat-ui"）</summary>
    [JsonPropertyName("clientId")]
    public string? ClientId { get; init; }

    /// <summary>客户端运行模式（如 "cli"、"ui"、"backend"）</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    /// <summary>设备平台标识（如 "MacIntel"、"Win32"、"Linux"）</summary>
    [JsonPropertyName("platform")]
    public string? Platform { get; init; }

    /// <summary>发起请求的客户端 IP 地址</summary>
    [JsonPropertyName("ip")]
    public string? Ip { get; init; }

    /// <summary>配对请求创建的时间戳（Unix 毫秒）</summary>
    [JsonPropertyName("ts")]
    public long? Ts { get; init; }
}

/// <summary>
/// device.pair.approve 接口的请求参数。
/// </summary>
public sealed record PairApproveParams
{
    /// <summary>要批准的配对请求 ID</summary>
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }
}

/// <summary>
/// device.pair.approve 接口的响应体。
/// </summary>
public sealed record PairApproveResponse
{
    /// <summary>审批操作是否成功</summary>
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }
}
