using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Tiki.Shared.HealthChecks;

/// <summary>Readiness check that asks the broker for cluster metadata with a short timeout — flips unhealthy within one check interval of a Redpanda outage.</summary>
public sealed class RedpandaHealthCheck(string bootstrapServers) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));

            var result = metadata.Brokers.Count > 0
                ? HealthCheckResult.Healthy($"{metadata.Brokers.Count} broker(s) reachable.")
                : HealthCheckResult.Unhealthy("No Redpanda brokers reachable.");

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Redpanda connectivity check failed.", ex));
        }
    }
}
