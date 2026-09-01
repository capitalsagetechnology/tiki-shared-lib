using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Tiki.Shared.HealthChecks;

/// <summary>Readiness check that connects and pings Redis — flips unhealthy within one check interval of an outage.</summary>
public sealed class RedisHealthCheck(string connectionString) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
            var latency = await connection.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Ping {latency.TotalMilliseconds:F0}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis connectivity check failed.", ex);
        }
    }
}
