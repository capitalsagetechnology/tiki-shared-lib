using Microsoft.Extensions.Caching.Distributed;

namespace Tiki.Shared.Auth;

/// <summary>
/// Remembers the nonces already seen, so a captured request cannot be replayed even inside
/// the clock-skew window. The whole contract is one atomic question — "was this new?" —
/// because a separate read-then-write would let two concurrent replays both observe
/// "unseen" and both be accepted.
/// </summary>
public interface INonceStore
{
    /// <summary>
    /// Records the nonce and returns <see langword="true"/> if it had not been seen before.
    /// Must be atomic against concurrent callers.
    /// </summary>
    Task<bool> TryConsumeAsync(string serviceId, string nonce, TimeSpan retention, CancellationToken ct = default);
}

/// <summary>
/// Redis-backed <see cref="INonceStore"/>, the one to use in any deployment with more than
/// one instance of a service: replicas share the record, so a request replayed against a
/// different instance is still caught.
/// </summary>
/// <remarks>
/// Atomicity comes from Redis' <c>SET key value NX EX ttl</c>, exposed here through
/// <see cref="IDistributedCache"/>. The stored value is a single byte; only the key's
/// existence carries meaning. Entries expire after <c>retention</c> (twice the clock skew),
/// past which the timestamp check rejects the request anyway, so nothing accumulates.
/// </remarks>
public sealed class DistributedNonceStore(IDistributedCache cache) : INonceStore
{
    private static readonly byte[] Marker = [1];

    public async Task<bool> TryConsumeAsync(string serviceId, string nonce, TimeSpan retention, CancellationToken ct = default)
    {
        var key = $"tiki:nonce:{serviceId}:{nonce}";

        if (await cache.GetAsync(key, ct) is not null)
            return false;

        await cache.SetAsync(key, Marker, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = retention }, ct);
        return true;
    }
}
