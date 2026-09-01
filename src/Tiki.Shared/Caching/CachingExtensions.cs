using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tiki.Shared.Caching;

public static class CachingExtensions
{
    /// <summary>
    /// Registers <see cref="ITieredCache"/> — L1 <c>IMemoryCache</c> plus L2 Redis via
    /// <c>Tiki:Caching:RedisConnectionString</c> (falling back to
    /// <c>ConnectionStrings:Redis</c>) — keyed under <paramref name="serviceName"/> so this
    /// service's cache entries never collide with another service's on the same Redis instance.
    /// </summary>
    public static IServiceCollection AddTikiCache(
        this IServiceCollection services, string serviceName, IConfiguration configuration)
    {
        var section = configuration.GetSection(TieredCacheOptions.SectionName);

        var redisConnectionString = section[nameof(TieredCacheOptions.RedisConnectionString)]
            ?? configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "Redis connection string not configured. Set 'Tiki:Caching:RedisConnectionString' or 'ConnectionStrings:Redis'.");

        services.Configure<TieredCacheOptions>(options =>
        {
            options.ServiceName = serviceName;
            options.RedisConnectionString = redisConnectionString;

            if (TimeSpan.TryParse(section[nameof(TieredCacheOptions.DefaultL1Ttl)], out var l1Ttl))
                options.DefaultL1Ttl = l1Ttl;

            if (TimeSpan.TryParse(section[nameof(TieredCacheOptions.DefaultL2Ttl)], out var l2Ttl))
                options.DefaultL2Ttl = l2Ttl;
        });

        services.AddMemoryCache();
        services.AddStackExchangeRedisCache(redis => redis.Configuration = redisConnectionString);
        services.AddSingleton<ITieredCache, TieredCache>();

        return services;
    }
}
