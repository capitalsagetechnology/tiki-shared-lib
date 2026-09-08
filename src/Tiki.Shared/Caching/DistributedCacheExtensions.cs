using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Tiki.Shared.Extensions;

namespace Tiki.Shared.Caching;

/// <summary>
/// Synchronous/async read helpers on the raw <see cref="IDistributedCache"/> — for a caller
/// that wrote through <see cref="ITieredCache.SetAsync{T}"/> at a specific key and needs to
/// read exactly that key back without going through the two-tier read path (which would
/// backfill L1, invoke no factory, and still require the same key already be prefixed).
/// </summary>
public static class DistributedCacheExtensions
{
    public static bool TryGetValue<T>(this IDistributedCache cache, string key, out T? value)
    {
        var bytes = cache.Get(key);
        if (bytes is null)
        {
            value = default;
            return false;
        }

        value = JsonSerializer.Deserialize<T>(bytes, TikiJson.Options);
        return true;
    }

    public static async Task<(bool Found, T? Value)> TryGetValueAsync<T>(
        this IDistributedCache cache, string key, CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(key, ct);
        if (bytes is null)
            return (false, default);

        var value = JsonSerializer.Deserialize<T>(bytes, TikiJson.Options);
        return (true, value);
    }
}
