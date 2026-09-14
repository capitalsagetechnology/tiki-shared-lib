using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Tiki.Shared.Extensions;

namespace Tiki.Shared.Caching;

/// <inheritdoc cref="ITieredCache"/>
public sealed class TieredCache(
    IMemoryCache l1,
    IDistributedCache l2,
    IConnectionMultiplexer redis,
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

    public async Task SetAsync<T>(string key, T value, CacheTier tier = CacheTier.L2, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var fullKey = BuildKey(key);

        if (tier == CacheTier.L1)
        {
            l1.Set(fullKey, value, ttl ?? _options.DefaultL1Ttl);
            return;
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, TikiJson.Options);
        await l2.SetAsync(
            fullKey, bytes,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl ?? _options.DefaultL2Ttl },
            ct);
    }

    public async Task InvalidateAsync(string key, CancellationToken ct = default)
    {
        var fullKey = BuildKey(key);
        l1.Remove(fullKey);
        await l2.RemoveAsync(fullKey, ct);
    }

    public Task SetOnceAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var fullKey = BuildKey(key);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, TikiJson.Options);

        // A plain Redis string, written directly — not through IDistributedCache, whose
        // StackExchangeRedis implementation stores entries as a hash (fields "data"/"absexp"/
        // "sldexp"). TryConsumeAsync's condition below only evaluates against a string value.
        return redis.GetDatabase().StringSetAsync(fullKey, bytes, ttl);
    }

    public async Task<bool> TryConsumeAsync<T>(string key, T expectedValue, CancellationToken ct = default)
    {
        var fullKey = BuildKey(key);
        var expectedBytes = JsonSerializer.SerializeToUtf8Bytes(expectedValue, TikiJson.Options);

        // GETDEL (StringGetDeleteAsync) can't do this: it deletes unconditionally and hands
        // back whatever was there, so a wrong guess would destroy the real value before its
        // owner ever submits it — turning "two callers race the same code" into "one wrong
        // guess denies the legitimate caller." A transaction with a value condition is
        // StackExchange.Redis's own C# primitive for a delete that only commits if the value
        // still matches when the server checks — the check and the delete happen as one
        // server-side unit, so a caller presenting the wrong value never deletes anything.
        //
        // SER301 below suggests StringDeleteAsync(key, ValueCondition.Equal(...)) instead —
        // that needs Redis 8.4+, and tiki-shared-infra pins the floating `redis:8-alpine` tag,
        // so it isn't a safe assumption yet. Revisit once the pinned version is confirmed.
#pragma warning disable SER301
        var database = redis.GetDatabase();
        var transaction = database.CreateTransaction();
        transaction.AddCondition(Condition.StringEqual(fullKey, expectedBytes));
        var delete = transaction.KeyDeleteAsync(fullKey);
        var committed = await transaction.ExecuteAsync();
#pragma warning restore SER301

        if (!committed)
            return false;

        await delete;
        l1.Remove(fullKey);
        return true;
    }

    private void BackfillL1<T>(string fullKey, T value, TieredCacheEntryOptions entryOptions)
    {
        if (entryOptions.L1Ttl > TimeSpan.Zero)
            l1.Set(fullKey, value, entryOptions.L1Ttl);
    }

    /// <summary><c>{service}:{key}</c> — <paramref name="key"/> is expected to already be <c>{entity}:{id}:{version}</c>-shaped.</summary>
    private string BuildKey(string key) => $"{_options.ServiceName}:{key}";
}
