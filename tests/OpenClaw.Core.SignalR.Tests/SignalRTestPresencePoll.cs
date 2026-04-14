using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR;
using OpenClaw.Core.SignalR;
using Xunit;

namespace OpenClaw.Tests.SignalR;

internal static class SignalRTestPresencePoll
{
    public static async Task AssertOnlineCountEventuallyAsync<THub>(
        IOpenClawSignalROperationService<THub> ops,
        int expected,
        int timeoutMs = 5000)
        where THub : Hub
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await ops.GetOnlineConnectionCountAsync().ConfigureAwait(false) == expected)
                return;
            await Task.Delay(50).ConfigureAwait(false);
        }

        Assert.Equal(expected, await ops.GetOnlineConnectionCountAsync().ConfigureAwait(false));
    }

    public static async Task AssertHttpConnectionCountEventuallyAsync(
        HttpClient http,
        string relativeUri,
        int expected,
        int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var n = await http.GetFromJsonAsync<int>(relativeUri).ConfigureAwait(false);
            if (n == expected)
                return;
            await Task.Delay(50).ConfigureAwait(false);
        }

        var final = await http.GetFromJsonAsync<int>(relativeUri).ConfigureAwait(false);
        Assert.Equal(expected, final);
    }

    public static async Task<IReadOnlyList<OpenClawSignalRConnectionSnapshot>> WaitForSnapshotsNonEmptyAsync<THub>(
        IOpenClawSignalROperationService<THub> ops,
        int timeoutMs = 5000)
        where THub : Hub
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var list = await ops.GetOnlineConnectionsAsync().ConfigureAwait(false);
            if (list.Count > 0)
                return list;
            await Task.Delay(50).ConfigureAwait(false);
        }

        return await ops.GetOnlineConnectionsAsync().ConfigureAwait(false);
    }

    /// <summary>轮询 HTTP JSON 数组端点直至非空（缓解 Hub 建连与运营快照写入的竞态）。</summary>
    public static async Task<List<T>> WaitForNonEmptyJsonListAsync<T>(
        HttpClient http,
        string relativeUri,
        int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var list = await http.GetFromJsonAsync<List<T>>(relativeUri).ConfigureAwait(false);
            if (list is { Count: > 0 })
                return list;
            await Task.Delay(50).ConfigureAwait(false);
        }

        var final = await http.GetFromJsonAsync<List<T>>(relativeUri).ConfigureAwait(false);
        Assert.NotNull(final);
        Assert.True(final!.Count > 0, $"Expected non-empty JSON list from {relativeUri} within {timeoutMs}ms");
        return final;
    }
}
