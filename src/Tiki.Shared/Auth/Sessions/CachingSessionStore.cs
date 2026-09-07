using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Tiki.Shared.Auth.Sessions;

/// <summary>
/// Puts a very short in-process window in front of <see cref="RedisSessionStore"/> reads.
///
/// <para>
/// Without it, every authenticated request on every service is a Redis round trip, and a
/// single page load that fans out to four services pays for four. With a five-second
/// window, a burst of requests from one user costs one. Redis is fast, but "fast" times
/// "every request on every service" is still the busiest thing in the platform.
/// </para>
///
/// <para>
/// Writes are deliberately <em>not</em> cached and always invalidate: revoking a session
/// drops it from this process's memory immediately, so a user logging out and the service
/// that handled the logout are never inconsistent. Other replicas converge within
/// <see cref="SessionOptions.CacheWindow"/> — see the note there on that trade-off.
/// </para>
/// </summary>
public sealed class CachingSessionStore(
    RedisSessionStore inner,
    IMemoryCache cache,
    IOptions<SessionOptions> options) : ISessionStore
{
    private readonly SessionOptions _options = options.Value;

    private static string CacheKey(Guid sessionId) => $"tiki:session-l1:{sessionId:N}";

    public async Task<TikiSession?> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (_options.CacheWindow <= TimeSpan.Zero)
            return await inner.GetAsync(sessionId, ct);

        var key = CacheKey(sessionId);
        if (cache.TryGetValue(key, out CachedSession? cached))
            return cached!.Session;

        var session = await inner.GetAsync(sessionId, ct);

        // A miss is cached too, wrapped rather than stored as a bare null. A token pointing
        // at a revoked session is exactly what a replay attempt looks like, and it would
        // otherwise hit Redis on every single retry.
        cache.Set(key, new CachedSession(session), _options.CacheWindow);

        return session;
    }

    public async Task CreateAsync(TikiSession session, CancellationToken ct = default)
    {
        await inner.CreateAsync(session, ct);
        cache.Remove(CacheKey(session.SessionId));
    }

    public async Task<bool> RevokeAsync(Guid sessionId, CancellationToken ct = default)
    {
        var revoked = await inner.RevokeAsync(sessionId, ct);
        cache.Remove(CacheKey(sessionId));
        return revoked;
    }

    public async Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var sessions = await inner.GetUserSessionsAsync(userId, ct);
        var revoked = await inner.RevokeAllForUserAsync(userId, ct);

        foreach (var session in sessions)
            cache.Remove(CacheKey(session.SessionId));

        return revoked;
    }

    public Task<IReadOnlyList<TikiSession>> GetUserSessionsAsync(Guid userId, CancellationToken ct = default) =>
        inner.GetUserSessionsAsync(userId, ct);

    public async Task<int> UpdatePermissionsAsync(Guid userId, IReadOnlyList<string> permissions, CancellationToken ct = default)
    {
        var sessions = await inner.GetUserSessionsAsync(userId, ct);
        var updated = await inner.UpdatePermissionsAsync(userId, permissions, ct);

        foreach (var session in sessions)
            cache.Remove(CacheKey(session.SessionId));

        return updated;
    }

    public Task TouchAsync(Guid sessionId, TimeSpan extendBy, CancellationToken ct = default) =>
        inner.TouchAsync(sessionId, extendBy, ct);

    /// <summary>Wrapper so "known to be absent" and "not looked up yet" are different cache states.</summary>
    private sealed record CachedSession(TikiSession? Session);
}
