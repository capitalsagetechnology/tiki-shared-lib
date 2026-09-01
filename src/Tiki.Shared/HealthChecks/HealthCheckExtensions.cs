using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tiki.Shared.HealthChecks;

public static class HealthCheckExtensions
{
    private const string ReadyTag = "ready";

    /// <summary>
    /// Registers readiness checks for whichever of Postgres, Redis, and Redpanda this
    /// service is configured for, reading connectivity from <c>ConnectionStrings:Postgres</c>,
    /// <c>ConnectionStrings:Redis</c> (falling back to <c>Tiki:Caching:RedisConnectionString</c>),
    /// and <c>Tiki:Messaging:BootstrapServers</c>. Call <see cref="MapTikiHealthChecks"/> to
    /// expose <c>/health/live</c> and <c>/health/ready</c> with identical shape across every service.
    /// </summary>
    public static IServiceCollection AddTikiHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddHealthChecks();

        var postgresConnectionString = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(postgresConnectionString))
            builder.AddCheck("postgres", new PostgresHealthCheck(postgresConnectionString), tags: [ReadyTag]);

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? configuration["Tiki:Caching:RedisConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
            builder.AddCheck("redis", new RedisHealthCheck(redisConnectionString), tags: [ReadyTag]);

        var bootstrapServers = configuration["Tiki:Messaging:BootstrapServers"];
        if (!string.IsNullOrWhiteSpace(bootstrapServers))
            builder.AddCheck("redpanda", new RedpandaHealthCheck(bootstrapServers), tags: [ReadyTag]);

        return services;
    }

    /// <summary><c>/health/live</c> answers "is the process up" with no dependency checks; <c>/health/ready</c> runs every check tagged <c>ready</c>.</summary>
    public static IEndpointRouteBuilder MapTikiHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });

        return endpoints;
    }
}
