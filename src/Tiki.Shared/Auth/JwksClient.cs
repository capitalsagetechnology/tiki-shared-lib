using System.Collections.Concurrent;
using Microsoft.IdentityModel.Tokens;

namespace Tiki.Shared.Auth;

/// <summary>
/// Fetches and caches the JSON Web Key Set exposed by Identity Service's OAuth2
/// authorization server. Not wired to a concrete <see cref="IServiceTokenProvider"/> in v1
/// — it exists so the real OAuth2 client-credentials provider that eventually replaces
/// <see cref="HmacServiceTokenProvider"/> has signing-key resolution ready to use, without
/// requiring a package-level change to adopt.
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
