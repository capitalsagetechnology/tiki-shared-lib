using StackExchange.Redis;

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
/// Redis-backed <see cref="INonceStore"/> — the one to use wherever a service runs as more
/// than one instance, since replicas share the record and a request replayed against a
/// different instance is still caught.
/// </summary>
/// <remarks>
/// Atomicity comes from <c>SET key value NX EX ttl</c>: Redis sets the key only if it does
/// not already exist, and reports whether it did. One command, so two concurrent replays
/// cannot both be told the nonce was new — which a <c>GET</c> followed by a <c>SET</c>
/// would allow, and which is the entire point of the store.
///
/// <para>
/// The value stored is a single byte; only the key's existence carries meaning. Entries
/// expire after <c>retention</c> (twice the clock skew), past which the timestamp check
/// rejects the request anyway — so nothing accumulates.
/// </para>
/// </remarks>
public sealed class RedisNonceStore(IConnectionMultiplexer redis) : INonceStore
{
    public Task<bool> TryConsumeAsync(string serviceId, string nonce, TimeSpan retention, CancellationToken ct = default) =>
        redis.GetDatabase().StringSetAsync(
            $"tiki:nonce:{serviceId}:{nonce}",
            value: 1,
            expiry: retention,
            when: When.NotExists);
}
