using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Tiki.Shared.Extensions;

namespace Tiki.Shared.Caching;

/// <inheritdoc cref="ITieredCache"/>
public sealed class TieredCache(
    IMemoryCache l1,
    IDistributedCache l2,
    IOptions<TieredCacheOptions> options) : ITieredCache
{
    private readonly TieredCacheOptions _options = options.Value;

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TieredCacheEntryOptions? entryOptions = null,
        CancellationToken ct = default)
    {
        entryOptions ??= TieredCacheEntryOptions.Default(_options);
        var fullKey = BuildKey(key);

        if (entryOptions.L1Ttl > TimeSpan.Zero && l1.TryGetValue(fullKey, out T? l1Value) && l1Value is not null)
            return l1Value;

        var l2Bytes = await l2.GetAsync(fullKey, ct);
        if (l2Bytes is not null)
        {
            var l2Value = JsonSerializer.Deserialize<T>(l2Bytes, TikiJson.Options)!;
            BackfillL1(fullKey, l2Value, entryOptions);
            return l2Value;
        }

        var value = await factory(ct);
        BackfillL1(fullKey, value, entryOptions);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, TikiJson.Options);
        await l2.SetAsync(
            fullKey, bytes,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = entryOptions.L2Ttl },
            ct);

        return value;
    }

    public async Task InvalidateAsync(string key, CancellationToken ct = default)
    {
        var fullKey = BuildKey(key);
        l1.Remove(fullKey);
        await l2.RemoveAsync(fullKey, ct);
    }

    private void BackfillL1<T>(string fullKey, T value, TieredCacheEntryOptions entryOptions)
    {
        if (entryOptions.L1Ttl > TimeSpan.Zero)
            l1.Set(fullKey, value, entryOptions.L1Ttl);
    }

    /// <summary><c>{service}:{key}</c> — <paramref name="key"/> is expected to already be <c>{entity}:{id}:{version}</c>-shaped.</summary>
    private string BuildKey(string key) => $"{_options.ServiceName}:{key}";
}
