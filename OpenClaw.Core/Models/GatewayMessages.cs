using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Core.Models;

/// <summary>
/// 网关 WebSocket 帧的抽象基类，所有帧都包含 type 字段。
/// </summary>
public abstract record GatewayFrame
{
    /// <summary>帧类型标识（"req" / "res" / "event"）</summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

/// <summary>
/// 客户端发往网关的 RPC 请求帧。
/// </summary>
public sealed record GatewayRequest : GatewayFrame
{
    /// <summary>帧类型，固定为 "req"</summary>
    [JsonPropertyName("type")]
    public override string Type => "req";

    /// <summary>请求唯一标识（UUID），用于关联响应</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>要调用的 RPC 方法名（如 "chat.send"、"health"）</summary>
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    /// <summary>RPC 方法的参数载荷（JSON 对象）</summary>
    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}

/// <summary>
/// 网关返回的 RPC 响应帧，与请求通过 Id 关联。
/// </summary>
public sealed record GatewayResponse
{
    /// <summary>帧类型，固定为 "res"</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "res";

    /// <summary>与请求对应的唯一标识（UUID）</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    /// <summary>请求是否成功</summary>
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    /// <summary>成功时的响应数据载荷</summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    /// <summary>失败时的错误详情</summary>
    [JsonPropertyName("error")]
    public JsonElement? Error { get; init; }
}

/// <summary>
/// 网关主动推送的事件帧。
/// </summary>
public sealed record GatewayEvent
{
    /// <summary>帧类型，固定为 "event"</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "event";

    /// <summary>事件名称（如 "agent"、"chat"、"presence"、"health"）</summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = "";

    /// <summary>事件携带的数据载荷</summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    /// <summary>事件序列号，用于检测丢包和保序</summary>
    [JsonPropertyName("seq")]
    public long? Seq { get; init; }

    /// <summary>随事件下发的状态版本号，供客户端增量同步</summary>
    [JsonPropertyName("stateVersion")]
    public StateVersionInfo? StateVersion { get; init; }
}

/// <summary>
/// WebSocket 接收到的原始帧，包含所有可能的字段，先反序列化再按 type 分发。
/// </summary>
public sealed record RawFrame
{
    /// <summary>帧类型（"req" / "res" / "event"）</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    /// <summary>请求/响应的唯一标识</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>RPC 方法名（仅 req 帧存在）</summary>
    [JsonPropertyName("method")]
    public string? Method { get; init; }

    /// <summary>响应是否成功（仅 res 帧存在）</summary>
    [JsonPropertyName("ok")]
    public bool? Ok { get; init; }

    /// <summary>事件名称（仅 event 帧存在）</summary>
    [JsonPropertyName("event")]
    public string? Event { get; init; }

    /// <summary>响应数据或事件数据载荷</summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    /// <summary>错误详情（仅 res 帧且失败时存在）</summary>
    [JsonPropertyName("error")]
    public JsonElement? Error { get; init; }

    /// <summary>事件序列号（仅 event 帧存在）</summary>
    [JsonPropertyName("seq")]
    public long? Seq { get; init; }

    /// <summary>状态版本号（仅 event 帧存在）</summary>
    [JsonPropertyName("stateVersion")]
    public StateVersionInfo? StateVersion { get; init; }

    /// <summary>RPC 参数载荷（仅 req 帧存在）</summary>
    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}