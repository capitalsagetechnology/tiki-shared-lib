using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tiki.Shared.Extensions;

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

    /// <summary>
    /// Maps <c>/health/live</c> — "is the process up", no dependency checks — and
    /// <c>/health/ready</c> — "can it serve traffic", every check tagged <c>ready</c>. Both
    /// answer <c>application/json</c> in the shape of <see cref="HealthReportResponse"/>.
    /// </summary>
    /// <remarks>
    /// The two are deliberately different questions, and an orchestrator acts on them
    /// differently: liveness failing means restart me, readiness failing means stop sending me
    /// traffic. Conflating them turns a transient Postgres blip into a restart loop — every
    /// replica killed at once, for an outage restarting cannot fix. So <c>/health/live</c>
    /// checks nothing external, and says so by returning no <c>checks</c> object at all rather
    /// than an empty one.
    ///
    /// <para>
    /// Status codes are unchanged from the ASP.NET defaults, because that is what container
    /// healthchecks and load balancers read: healthy and degraded are 200, unhealthy is 503.
    /// The body is for whoever is reading the failure, not for the machine acting on it.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The route builder.</param>
    /// <param name="serviceName">
    /// Stamped onto every response. Defaults to <c>Tiki:Telemetry:ServiceName</c> — the same name
    /// the service reports on every span and log line, so a health body and a trace agree.
    /// </param>
    public static IEndpointRouteBuilder MapTikiHealthChecks(
        this IEndpointRouteBuilder endpoints, string? serviceName = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        serviceName ??= endpoints.ServiceProvider
            .GetService<IConfiguration>()?["Tiki:Telemetry:ServiceName"];

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = (context, report) => WriteAsync(context, report, serviceName, includeChecks: false),
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = (context, report) => WriteAsync(context, report, serviceName, includeChecks: true),
        });

        return endpoints;
    }

    /// <summary>
    /// Serialises a report. Public so a host that maps the endpoints itself — a worker, which
    /// has no <c>IEndpointRouteBuilder</c> of the usual shape — gets the same body rather than
    /// a second, nearly-identical one.
    /// </summary>
    public static Task WriteAsync(
        HttpContext context, HealthReport report, string? serviceName = null, bool includeChecks = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);

        var response = new HealthReportResponse
        {
            Status = report.Status,
            Service = serviceName,
            CheckedAt = DateTimeOffset.UtcNow,
            TotalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            Checks = includeChecks
                ? report.Entries.ToDictionary(
                    entry => entry.Key,
                    entry => new HealthCheckEntry
                    {
                        Status = entry.Value.Status,
                        Description = entry.Value.Description,
                        DurationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),

                        // Type name only — never the message. A driver exception carries the
                        // connection it failed on, and this endpoint is unauthenticated.
                        Error = entry.Value.Exception?.GetType().Name,
                    },
                    StringComparer.OrdinalIgnoreCase)
                : null,
        };

        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, TikiJson.Options));
    }
}
