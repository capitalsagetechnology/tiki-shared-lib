using System.Collections.Concurrent;
using Microsoft.IdentityModel.Tokens;

namespace Tiki.Shared.Auth;

/// <summary>
/// Fetches and caches the JSON Web Key Set published by Identity.
///
/// <para>
/// <see cref="TikiJwtExtensions.AddTikiJwtAuth"/> already resolves JWKS itself when
/// <see cref="TikiJwtOptions.JwksUri"/> is set, so nothing in the request path needs this
/// today. It stays for the callers that need keys outside an authentication handler — a
/// background worker validating a token off a Kafka message, say — and for the move from
/// the interim symmetric key to asymmetric signing, where a service should verify with a
/// public key and never hold a value capable of minting a token.
/// </para>
/// </summary>
public sealed class JwksClient(HttpClient httpClient)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, (JsonWebKeySet Keys, DateTimeOffset FetchedAt)> _cache = new();

    public async Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(string jwksUri, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(jwksUri, out var cached) && DateTimeOffset.UtcNow - cached.FetchedAt < CacheDuration)
            return [.. cached.Keys.GetSigningKeys()];

        var json = await httpClient.GetStringAsync(jwksUri, ct);
        var keySet = new JsonWebKeySet(json);
        _cache[jwksUri] = (keySet, DateTimeOffset.UtcNow);

        return [.. keySet.GetSigningKeys()];
    }
}
