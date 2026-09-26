namespace Tiki.Shared.Auth.ApiKeys;

/// <summary>
/// The projection of active API keys the gateway resolves <c>X-API-KEY</c> against. Identity is
/// the only writer; its database stays the source of truth and can rebuild this at any time.
/// </summary>
public interface IApiKeyStore
{
    /// <summary>The grant for a key hash, or null when the key is unknown, revoked or expired.</summary>
    Task<ApiKeyGrant?> GetAsync(string keyHash, CancellationToken ct = default);

    /// <summary>
    /// Replaces every grant a business has with <paramref name="grants"/> (hash → grant) in one
    /// step. Hashes the business held before and does not hold now are deleted, so revoking a
    /// key is simply re-projecting its business without it.
    /// </summary>
    Task ReplaceForBusinessAsync(Guid businessId, IReadOnlyDictionary<string, ApiKeyGrant> grants, CancellationToken ct = default);

    /// <summary>
    /// Records that a key was just used. Called by the gateway on the request path, so it is a
    /// single fire-and-forget write, never a read-modify-write.
    /// </summary>
    Task TouchAsync(Guid keyId, DateTimeOffset usedAt, CancellationToken ct = default);

    /// <summary>Last-use times for the given keys; keys never used are absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetLastUsedAsync(IReadOnlyCollection<Guid> keyIds, CancellationToken ct = default);
}
