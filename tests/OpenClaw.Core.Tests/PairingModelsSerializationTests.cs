using System.Text.Json;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Models;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// <see cref="PairListResponse"/> 等配对相关 DTO 的 JSON 往返测试，补齐仅通过 RPC 反射未触发的属性访问行。
/// </summary>
public sealed class PairingModelsSerializationTests
{
    /// <summary>
    /// <see cref="PairListResponse"/> 应能按网关字段名反序列化并保留三个列表。
    /// </summary>
    [Fact]
    public void PairListResponse_deserializes_three_buckets()
    {
        const string json = """{"pending":[{"requestId":"p1","deviceId":"d"}],"approved":[],"rejected":[]}""";
        var model = JsonSerializer.Deserialize<PairListResponse>(json, JsonDefaults.SerializerOptions);
        Assert.NotNull(model);
        Assert.Single(model.Pending);
        Assert.Equal("p1", model.Pending[0].RequestId);
        Assert.Empty(model.Approved);
        Assert.Empty(model.Rejected);
    }

    /// <summary>
    /// <see cref="PairApproveParams"/> / <see cref="PairApproveResponse"/> 应可序列化。
    /// </summary>
    [Fact]
    public void PairApprove_roundtrip()
    {
        var req = new PairApproveParams { RequestId = "r1" };
        var reqJson = JsonSerializer.Serialize(req, JsonDefaults.SerializerOptions);
        Assert.Contains("r1", reqJson);
        var resp = new PairApproveResponse { Ok = true };
        var respJson = JsonSerializer.Serialize(resp, JsonDefaults.SerializerOptions);
        Assert.Contains("true", respJson);
    }
}
