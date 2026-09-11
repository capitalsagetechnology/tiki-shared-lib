namespace Tiki.Shared.Caching;

/// <summary>Which tier a direct <see cref="ITieredCache.SetAsync{T}"/> write targets.</summary>
public enum CacheTier
{
    /// <summary>In-process <c>IMemoryCache</c> only — not visible to other replicas.</summary>
    L1,

    /// <summary>Distributed (Redis) only.</summary>
    L2,
}

/// <summary>
/// Two-tier cache — L1 (<see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>) and
/// L2 (<see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>/Redis),
/// always both, never a config flag choosing one. A read hits L1 first, falls through to
/// L2 and backfills L1 on an L2 hit, and computes-and-writes-through-both on a full miss.
/// </summary>
public interface ITieredCache
{
    /// <summary>
    /// Reads through L1 then L2; on a full miss, invokes <paramref name="factory"/> and
    /// writes the result through both tiers. Pass <see cref="TieredCacheEntryOptions.SkipL1"/>
    /// for a key that cannot tolerate cross-replica staleness.
    /// </summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TieredCacheEntryOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Writes directly to exactly the specified tier — unlike <see cref="GetOrSetAsync{T}"/>,
    /// which always writes through both. For a value with no "compute" step (an OTP code, a
    /// short-lived flag) where the caller already knows the value and just needs it cached.
    /// </summary>
    Task SetAsync<T>(
        string key,
        T value,
        CacheTier tier = CacheTier.L2,
        TimeSpan? ttl = null,
        CancellationToken ct = default);

    /// <summary>Removes the key from both L1 and L2.</summary>
    Task InvalidateAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Writes a value meant to be read exactly once, via <see cref="TryConsumeAsync{T}"/> — an
    /// OTP code, a single-use invite token, anything where a second successful read must not
    /// be possible. Backed by a plain Redis string via a direct connection, not
    /// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> — its on-wire
    /// format (a hash, via <c>Microsoft.Extensions.Caching.StackExchangeRedis</c>) has no
    /// atomic compare-and-delete, and isn't even the right Redis type for one. Never mix with
    /// <see cref="SetAsync{T}"/>/<see cref="GetOrSetAsync{T}"/> on the same key.
    /// </summary>
    Task SetOnceAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Atomically compares the value written by <see cref="SetOnceAsync{T}"/> at <paramref
    /// name="key"/> to <paramref name="expectedValue"/> and deletes it if — and only if — they
    /// match, in one round trip. Two callers racing the same correct value can never both
    /// succeed; a caller presenting the wrong value never deletes anything, so it cannot lock
    /// out whoever presents the right one moments later.
    /// </summary>
    Task<bool> TryConsumeAsync<T>(string key, T expectedValue, CancellationToken ct = default);
}
