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

    /// <summary>使用 <see cref="OpenClawSignalROptions.ConnectionPresenceIndexKey"/> 作为分布式键。</summary>
    public OpenClawSignalRDistributedConnectionIdIndex(
        IDistributedCache distributed,
        IOptions<OpenClawSignalROptions> options)
    {
        _distributed = distributed;
        _options = options;
    }

    /// <summary>读取分布式中保存的全部索引 token（JSON 字符串数组）。</summary>
    public async Task<IReadOnlyList<string>> GetAllIndexTokensAsync(CancellationToken cancellationToken = default)
    {
        var bytes = await _distributed.GetAsync(IndexKey, cancellationToken).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
            return [];

        var tokens = JsonSerializer.Deserialize<string[]>(bytes, JsonOptions);
        return tokens is { Length: > 0 } ? tokens : [];
    }

    /// <summary>在分布式集合中追加一个 token（读-改-写，非事务）。</summary>
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

    /// <summary>从分布式集合移除 token；空集时删除整键。</summary>
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

    /// <summary>分布式缓存中的索引键。</summary>
    private string IndexKey => _options.Value.ConnectionPresenceIndexKey;

    /// <summary>将 UTF-8 JSON 数组反序列化为去重集合。</summary>
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