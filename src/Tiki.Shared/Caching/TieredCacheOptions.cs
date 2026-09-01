namespace Tiki.Shared.Caching;

/// <summary>Options for <see cref="TieredCache"/>, bound from <c>IConfiguration</c> plus the calling service's own name.</summary>
public sealed class TieredCacheOptions
{
    public const string SectionName = "Tiki:Caching";

    /// <summary>
    /// This service's own name, used as the key prefix (<c>{service}:{entity}:{id}:{version}</c>)
    /// so one shared Redis instance is safe across every service in early environments.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    public TimeSpan DefaultL1Ttl { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan DefaultL2Ttl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Falls back to <c>ConnectionStrings:Redis</c> if not set here.</summary>
    public string? RedisConnectionString { get; set; }
}

/// <summary>Per-call TTL override for <see cref="ITieredCache.GetOrSetAsync{T}"/>.</summary>
public sealed class TieredCacheEntryOptions
{
    /// <summary><see cref="TimeSpan.Zero"/> makes this key L2-only — for values that cannot tolerate cross-replica staleness.</summary>
    public required TimeSpan L1Ttl { get; init; }
    public required TimeSpan L2Ttl { get; init; }

    public static TieredCacheEntryOptions Default(TieredCacheOptions options) => new()
    {
        L1Ttl = options.DefaultL1Ttl,
        L2Ttl = options.DefaultL2Ttl,
    };

    public static TieredCacheEntryOptions SkipL1(TieredCacheOptions options) => new()
    {
        L1Ttl = TimeSpan.Zero,
        L2Ttl = options.DefaultL2Ttl,
    };
}
