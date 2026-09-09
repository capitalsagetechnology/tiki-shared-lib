using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Tiki.Shared.Auth.Sessions;

namespace Tiki.Shared.Auth;

public static class AuthExtensions
{
    /// <summary>
    /// Registers the Redis-backed session store every service reads on each authenticated
    /// request, plus the short in-process cache in front of it.
    /// </summary>
    /// <remarks>
    /// Identity calls this and uses the write side; every other service calls it and only
    /// ever reads. Same registration either way, so there is no "am I the writer?" branch
    /// in any service's <c>Program.cs</c>.
    /// </remarks>
    public static IServiceCollection AddTikiSessions(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration["Tiki:Caching:RedisConnectionString"]
            ?? configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "Sessions require Redis. Set 'Tiki:Caching:RedisConnectionString' or 'ConnectionStrings:Redis'.");

        services.Configure<SessionOptions>(configuration.GetSection(SessionOptions.SectionName));
        services.TryAddTimeProvider();
        services.AddMemoryCache();
        services.TryAddRedis(redisConnectionString);

        services.AddSingleton<RedisSessionStore>();
        services.AddSingleton<ISessionStore>(sp => new CachingSessionStore(
            sp.GetRequiredService<RedisSessionStore>(),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
            sp.GetRequiredService<IOptions<SessionOptions>>()));

        return services;
    }

    /// <summary>
    /// Registers service-to-service request signing and verification: this service's
    /// identity, the callers it trusts, and the replay-protection store.
    /// </summary>
    /// <remarks>
    /// Falls back to <see cref="InMemoryNonceStore"/> when Redis is not configured, so a
    /// service still gets replay protection rather than silently getting none — but that
    /// fallback is only correct for a single instance, and the options doc says so.
    /// </remarks>
    public static IServiceCollection AddTikiServiceAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ServiceIdentityOptions>()
            .Bind(configuration.GetSection(ServiceIdentityOptions.SectionName))
            // Validated at startup, not at first use: a service with a missing or too-short
            // signing secret should fail to boot, not fail its first inter-service call.
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddTimeProvider();

        var redisConnectionString =
            configuration["Tiki:Caching:RedisConnectionString"] ?? configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.TryAddRedis(redisConnectionString);
            services.AddSingleton<INonceStore, RedisNonceStore>();
        }
        else
        {
            services.AddSingleton<INonceStore, InMemoryNonceStore>();
        }

        services.AddSingleton<HmacServiceRequestSigner>();
        services.AddSingleton<IServiceRequestSigner>(sp => sp.GetRequiredService<HmacServiceRequestSigner>());
        services.AddSingleton<IServiceRequestVerifier>(sp => sp.GetRequiredService<HmacServiceRequestSigner>());

        return services;
    }

    /// <summary>
    /// A typed <see cref="System.Net.Http.HttpClient"/> that signs and context-propagates
    /// every request it makes to another Tiki service.
    /// </summary>
    /// <remarks>
    /// Handler order is load-bearing: context propagation runs first so the identity headers
    /// exist before the signature is computed over them. Registered the other way round, the
    /// identity headers would travel outside the signature — that is, forgeable in transit.
    /// </remarks>
    public static IHttpClientBuilder AddTikiServiceClient<TClient>(
        this IServiceCollection services, Uri baseAddress)
        where TClient : class
    {
        services.AddTransient<Http.TikiContextPropagationHandler>();
        services.AddTransient<Http.ServiceRequestSigningHandler>();

        return services.AddHttpClient<TClient>(client => client.BaseAddress = baseAddress)
            .AddHttpMessageHandler<Http.TikiContextPropagationHandler>()
            .AddHttpMessageHandler<Http.ServiceRequestSigningHandler>();
    }

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
            services.AddSingleton(TimeProvider.System);
    }

    /// <summary>
    /// Registers one <see cref="IConnectionMultiplexer"/> for the process, once.
    /// </summary>
    /// <remarks>
    /// Guarded because <c>AddTikiSessions</c> and <c>AddTikiServiceAuth</c> both need Redis
    /// and most services call both. StackExchange.Redis is designed to be shared — a
    /// multiplexer per registration would open a second connection pool for no benefit, and
    /// per-scope would exhaust connections under load.
    /// </remarks>
    private static void TryAddRedis(this IServiceCollection services, string connectionString)
    {
        if (services.Any(d => d.ServiceType == typeof(IConnectionMultiplexer)))
            return;

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(connectionString);

            // Never abort on a failed first connection. The default (abortConnect=true) makes
            // Connect() throw when Redis is unreachable, and because this is a factory the
            // throw repeats on every resolution — ten seconds per request, forever, on every
            // endpoint that touches the multiplexer. Liveness included: /health/live answered
            // 500 for as long as Redis was down, so the orchestrator restarted a service whose
            // only problem was a dependency it could have waited for.
            //
            // With this off the multiplexer is created once, reconnects in the background, and
            // an operation attempted while Redis is down fails fast with a clear error. The
            // readiness probe is what reports the dependency; liveness stays about the process.
            options.AbortOnConnectFail = false;

            // Bounded, so the one blocking attempt at creation cannot stall startup.
            options.ConnectTimeout = Math.Min(options.ConnectTimeout, 5_000);
            options.ConnectRetry = Math.Max(options.ConnectRetry, 3);

            return ConnectionMultiplexer.Connect(options);
        });
    }
}
