using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Tiki.Shared.HealthChecks;

/// <summary>Readiness check that opens a connection and runs <c>SELECT 1</c> — flips unhealthy within one check interval of an outage.</summary>
public sealed class PostgresHealthCheck(string connectionString) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            await using var command = new NpgsqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync(ct);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Postgres connectivity check failed.", ex);
        }
    }
}
