using System.Globalization;
using System.Text.Json;
using StackExchange.Redis;

namespace Tiki.Shared.Auth.ApiKeys;

/// <summary>
/// Redis-backed <see cref="IApiKeyStore"/>. Three key shapes:
///
/// <list type="bullet">
/// <item><c>tiki:apikey:{hash}</c> — the grant JSON, expiring with the key when it has an end date.</item>
/// <item><c>tiki:business-apikeys:{businessId}</c> — the set of hashes a business holds, so a
/// re-projection knows which grants to delete without scanning the keyspace.</item>
/// <item><c>tiki:apikey-lastused:{keyId}</c> — unix milliseconds of the latest use.</item>
/// </list>
/// </summary>
public sealed class RedisApiKeyStore(IConnectionMultiplexer redis, TimeProvider timeProvider) : IApiKeyStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Long enough to answer "last used" for any key a business still looks at.</summary>
    private static readonly TimeSpan LastUsedRetention = TimeSpan.FromDays(400);

    private IDatabase Db => redis.GetDatabase();

    internal static string GrantKey(string keyHash) => $"tiki:apikey:{keyHash}";
    internal static string BusinessIndexKey(Guid businessId) => $"tiki:business-apikeys:{businessId:N}";
    internal static string LastUsedKey(Guid keyId) => $"tiki:apikey-lastused:{keyId:N}";

    public async Task<ApiKeyGrant?> GetAsync(string keyHash, CancellationToken ct = default)
    {
        var value = await Db.StringGetAsync(GrantKey(keyHash));
        if (value.IsNullOrEmpty)
            return null;

        var grant = JsonSerializer.Deserialize<ApiKeyGrant>(value.ToString(), Json);

        // The TTL removes an expired grant, but Redis expiry is lazy to the millisecond and a
        // rolled key must stop working at the moment the business was promised, not shortly after.
        return grant is null || grant.ExpiresAt <= timeProvider.GetUtcNow() ? null : grant;
    }

    public async Task ReplaceForBusinessAsync(
        Guid businessId, IReadOnlyDictionary<string, ApiKeyGrant> grants, CancellationToken ct = default)
    {
        var indexKey = BusinessIndexKey(businessId);
        var previous = await Db.SetMembersAsync(indexKey);
        var now = timeProvider.GetUtcNow();

        // MULTI/EXEC, so the gateway never observes a half-applied projection — in particular
        // never a moment where a revoked key's grant still exists after the index dropped it.
        var tx = Db.CreateTransaction();
        var pending = new List<Task>();

        foreach (var stale in previous.Select(h => h.ToString()).Where(h => !grants.ContainsKey(h)))
            pending.Add(tx.KeyDeleteAsync(GrantKey(stale)));

        pending.Add(tx.KeyDeleteAsync(indexKey));

        foreach (var (hash, grant) in grants)
        {
            if (grant.BusinessId != businessId)
                throw new ArgumentException($"Grant {grant.KeyId} belongs to business {grant.BusinessId}, not {businessId}.", nameof(grants));

            TimeSpan? ttl = grant.ExpiresAt is { } expiresAt ? expiresAt - now : null;
            if (ttl <= TimeSpan.Zero)
            {
                pending.Add(tx.KeyDeleteAsync(GrantKey(hash)));
                continue;
            }

            var json = JsonSerializer.Serialize(grant, Json);
            pending.Add(ttl is { } expiry
                ? tx.StringSetAsync(GrantKey(hash), json, expiry)
                : tx.StringSetAsync(GrantKey(hash), json));
            pending.Add(tx.SetAddAsync(indexKey, hash));
        }

        if (!await tx.ExecuteAsync())
            throw new InvalidOperationException($"Could not replace the API key projection for business {businessId}.");

        await Task.WhenAll(pending);
    }

    public Task TouchAsync(Guid keyId, DateTimeOffset usedAt, CancellationToken ct = default) =>
        Db.StringSetAsync(
            LastUsedKey(keyId),
            usedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            LastUsedRetention,
            flags: CommandFlags.FireAndForget);

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetLastUsedAsync(
        IReadOnlyCollection<Guid> keyIds, CancellationToken ct = default)
    {
        if (keyIds.Count == 0)
            return new Dictionary<Guid, DateTimeOffset>();

        var ids = keyIds.ToArray();
        var values = await Db.StringGetAsync(ids.Select(id => (RedisKey)LastUsedKey(id)).ToArray());

        var result = new Dictionary<Guid, DateTimeOffset>();
        for (var i = 0; i < ids.Length; i++)
        {
            if (long.TryParse(values[i].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
                result[ids[i]] = DateTimeOffset.FromUnixTimeMilliseconds(ms);
        }

        return result;
    }
}
