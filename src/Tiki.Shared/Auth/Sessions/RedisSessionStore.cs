using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Tiki.Shared.Extensions;

namespace Tiki.Shared.Auth.Sessions;

/// <summary>
/// Redis-backed <see cref="ISessionStore"/>. Two key shapes:
///
/// <list type="bullet">
/// <item><c>tiki:session:{sessionId}</c> — the session JSON, with a TTL matching its expiry.</item>
/// <item><c>tiki:user-sessions:{userId}</c> — a set of that user's session ids, so
/// "log out everywhere" and "change this user's permissions" do not require scanning the
/// keyspace. <c>KEYS</c>/<c>SCAN</c> over a production Redis is exactly the operation that
/// turns a login spike into an outage.</item>
/// </list>
/// </summary>
public sealed class RedisSessionStore(
    IConnectionMultiplexer redis,
    IOptions<SessionOptions> options,
    TimeProvider timeProvider,
    ILogger<RedisSessionStore> logger) : ISessionStore
{
    private readonly SessionOptions _options = options.Value;
    private IDatabase Db => redis.GetDatabase();

    private static string SessionKey(Guid sessionId) => $"tiki:session:{sessionId:N}";
    private static string UserSessionsKey(Guid userId) => $"tiki:user-sessions:{userId:N}";

    public async Task CreateAsync(TikiSession session, CancellationToken ct = default)
    {
        var ttl = session.ExpiresAt - timeProvider.GetUtcNow();
        if (ttl <= TimeSpan.Zero)
            throw new ArgumentException("Session already expired.", nameof(session));

        var userKey = UserSessionsKey(session.UserId);

        // One round trip, and the index can never end up referencing a session that was not
        // written — a batch is pipelined, so both commands reach Redis together.
        var batch = Db.CreateBatch();
        var write = batch.StringSetAsync(SessionKey(session.SessionId), Serialize(session), ttl);
        var index = batch.SetAddAsync(userKey, session.SessionId.ToString("N"));
        // The index outlives any single session in it, but not indefinitely — without an
        // expiry it would accumulate one key per user who ever logged in, forever.
        var expire = batch.KeyExpireAsync(userKey, _options.UserIndexRetention);
        batch.Execute();

        await Task.WhenAll(write, index, expire);

        if (_options.MaxSessionsPerUser > 0)
            await EnforceSessionLimitAsync(session.UserId, session.SessionId, ct);
    }

    public async Task<TikiSession?> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        var value = await Db.StringGetAsync(SessionKey(sessionId));
        return value.IsNullOrEmpty ? null : Deserialize(value!);
    }

    public async Task<bool> RevokeAsync(Guid sessionId, CancellationToken ct = default)
    {
        // Read first so the user index can be cleaned up too — a deleted session whose id
        // lingers in the index would be re-read as a null on every "log out everywhere".
        var session = await GetAsync(sessionId, ct);

        var deleted = await Db.KeyDeleteAsync(SessionKey(sessionId));

        if (session is not null)
            await Db.SetRemoveAsync(UserSessionsKey(session.UserId), sessionId.ToString("N"));

        if (deleted)
            logger.LogInformation("Session {SessionId} revoked.", sessionId);

        return deleted;
    }

    public async Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var userKey = UserSessionsKey(userId);
        var ids = await Db.SetMembersAsync(userKey);
        if (ids.Length == 0)
            return 0;

        var keys = ids.Select(id => (RedisKey)$"tiki:session:{id}").ToArray();
        var deleted = await Db.KeyDeleteAsync(keys);
        await Db.KeyDeleteAsync(userKey);

        logger.LogInformation("Revoked {Count} session(s) for user {UserId}.", deleted, userId);
        return (int)deleted;
    }

    public async Task<IReadOnlyList<TikiSession>> GetUserSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        var ids = await Db.SetMembersAsync(UserSessionsKey(userId));
        if (ids.Length == 0)
            return [];

        var values = await Db.StringGetAsync(ids.Select(id => (RedisKey)$"tiki:session:{id}").ToArray());

        // Entries that came back empty are sessions that expired since the index was last
        // touched. Prune them rather than returning holes.
        var live = new List<TikiSession>(values.Length);
        var stale = new List<RedisValue>();

        for (var i = 0; i < values.Length; i++)
        {
            if (values[i].IsNullOrEmpty)
                stale.Add(ids[i]);
            else
                live.Add(Deserialize(values[i]!));
        }

        if (stale.Count > 0)
            await Db.SetRemoveAsync(UserSessionsKey(userId), [.. stale]);

        return live;
    }

    public async Task<int> UpdateAccessAsync(
        Guid userId,
        IReadOnlyList<string> globalPermissions,
        IReadOnlyDictionary<Guid, TenantGrant> tenantAccess,
        CancellationToken ct = default)
    {
        var sessions = await GetUserSessionsAsync(userId, ct);
        if (sessions.Count == 0)
            return 0;

        var batch = Db.CreateBatch();
        var writes = sessions
            .Select(session => batch.StringSetAsync(
                SessionKey(session.SessionId),
                Serialize(session with { GlobalPermissions = globalPermissions, TenantAccess = tenantAccess }),
                expiry: null,
                // KeepTtl: the permission change must not silently extend how long the
                // session lives. Writing without it resets the key to no expiry at all.
                keepTtl: true))
            .ToArray();
        batch.Execute();

        await Task.WhenAll(writes);

        logger.LogInformation(
            "Refreshed access on {Count} live session(s) for user {UserId}: {TenantCount} tenant(s), {GlobalCount} global permission(s).",
            sessions.Count, userId, tenantAccess.Count, globalPermissions.Count);
        return sessions.Count;
    }

    public async Task TouchAsync(Guid sessionId, TimeSpan extendBy, CancellationToken ct = default)
    {
        // ExpireWhen.GreaterThanCurrentExpiry so a touch can only ever push expiry further
        // out. Without it, a slow request racing a fresher one would shorten the session.
        await Db.KeyExpireAsync(SessionKey(sessionId), extendBy, ExpireWhen.GreaterThanCurrentExpiry);
    }

    /// <summary>
    /// Caps concurrent sessions per user by evicting the oldest. A user whose credentials
    /// are being used from an unbounded number of places is the signal this exists for;
    /// it also stops one account pinning an unbounded amount of Redis.
    /// </summary>
    private async Task EnforceSessionLimitAsync(Guid userId, Guid keepSessionId, CancellationToken ct)
    {
        var sessions = await GetUserSessionsAsync(userId, ct);
        if (sessions.Count <= _options.MaxSessionsPerUser)
            return;

        var toEvict = sessions
            .Where(s => s.SessionId != keepSessionId)
            .OrderBy(s => s.IssuedAt)
            .Take(sessions.Count - _options.MaxSessionsPerUser);

        foreach (var session in toEvict)
        {
            await RevokeAsync(session.SessionId, ct);
            logger.LogInformation(
                "Evicted session {SessionId} for user {UserId}: session limit of {Limit} reached.",
                session.SessionId, userId, _options.MaxSessionsPerUser);
        }
    }

    private static string Serialize(TikiSession session) => JsonSerializer.Serialize(session, TikiJson.Options);

    private static TikiSession Deserialize(string json) =>
        JsonSerializer.Deserialize<TikiSession>(json, TikiJson.Options)
        ?? throw new InvalidOperationException("Stored session could not be deserialized.");
}
