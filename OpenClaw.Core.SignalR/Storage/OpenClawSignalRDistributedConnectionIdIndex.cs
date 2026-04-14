using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace OpenClaw.Core.SignalR;

/// <summary>
/// 使用 <see cref="IDistributedCache"/> 保存索引 token 列表（JSON），每项为 <c>userKeySegment|connectionId</c>，供 Hybrid 连接存储枚举。
/// </summary>
public sealed class OpenClawSignalRDistributedConnectionIdIndex
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDistributedCache _distributed;
    private readonly IOptions<OpenClawSignalROptions> _options;

    public OpenClawSignalRDistributedConnectionIdIndex(
        IDistributedCache distributed,
        IOptions<OpenClawSignalROptions> options)
    {
        _distributed = distributed;
        _options = options;
    }

    public async Task<IReadOnlyList<string>> GetAllIndexTokensAsync(CancellationToken cancellationToken = default)
    {
        var bytes = await _distributed.GetAsync(IndexKey, cancellationToken).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
            return [];

        var tokens = JsonSerializer.Deserialize<string[]>(bytes, JsonOptions);
        return tokens is { Length: > 0 } ? tokens : [];
    }

    public async Task AddAsync(string indexToken, CancellationToken cancellationToken = default)
    {
        var bytes = await _distributed.GetAsync(IndexKey, cancellationToken).ConfigureAwait(false);
        var set = ToSet(bytes);
        if (!set.Add(indexToken))
            return;

        var next = JsonSerializer.SerializeToUtf8Bytes(set.ToArray(), JsonOptions);
        await _distributed.SetAsync(IndexKey, next, new DistributedCacheEntryOptions(), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RemoveAsync(string indexToken, CancellationToken cancellationToken = default)
    {
        var bytes = await _distributed.GetAsync(IndexKey, cancellationToken).ConfigureAwait(false);
        var set = ToSet(bytes);
        if (!set.Remove(indexToken))
            return;

        if (set.Count == 0)
        {
            await _distributed.RemoveAsync(IndexKey, cancellationToken).ConfigureAwait(false);
            return;
        }

        var next = JsonSerializer.SerializeToUtf8Bytes(set.ToArray(), JsonOptions);
        await _distributed.SetAsync(IndexKey, next, new DistributedCacheEntryOptions(), cancellationToken)
            .ConfigureAwait(false);
    }

    private string IndexKey => _options.Value.ConnectionPresenceIndexKey;

    private static HashSet<string> ToSet(byte[]? bytes)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (bytes is null || bytes.Length == 0)
            return set;
        var arr = JsonSerializer.Deserialize<string[]>(bytes, JsonOptions);
        if (arr is null)
            return set;
        foreach (var id in arr)
        {
            if (!string.IsNullOrEmpty(id))
                set.Add(id);
        }

        return set;
    }
}