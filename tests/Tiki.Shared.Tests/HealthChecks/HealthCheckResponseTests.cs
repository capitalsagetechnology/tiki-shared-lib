using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tiki.Shared.HealthChecks;
using Xunit;

namespace Tiki.Shared.Tests.HealthChecks;

/// <summary>
/// Covers the body <c>/health/live</c> and <c>/health/ready</c> return. No live Postgres, Redis or
/// Redpanda: a <see cref="HealthReport"/> is a plain value, so the writer can be driven directly.
/// </summary>
public class HealthCheckResponseTests
{
    private static HealthReport Report(HealthStatus status, params (string Name, HealthReportEntry Entry)[] entries) =>
        new(entries.ToDictionary(e => e.Name, e => e.Entry, StringComparer.OrdinalIgnoreCase),
            status,
            TimeSpan.FromMilliseconds(12.345));

    private static HealthReportEntry Entry(
        HealthStatus status, string? description = null, Exception? exception = null, double ms = 3.21) =>
        new(status, description, TimeSpan.FromMilliseconds(ms), exception, data: null);

    private static async Task<JsonElement> WriteAsync(
        HealthReport report, bool includeChecks, string? serviceName = "wallet-service")
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthCheckExtensions.WriteAsync(context, report, serviceName, includeChecks);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = JsonDocument.Parse(context.Response.Body);
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Live_returns_json_with_no_checks_object()
    {
        var json = await WriteAsync(Report(HealthStatus.Healthy), includeChecks: false);

        Assert.Equal("healthy", json.GetProperty("status").GetString());
        Assert.Equal("wallet-service", json.GetProperty("service").GetString());
        Assert.True(json.TryGetProperty("checkedAt", out _));

        // Absent, not empty: /health/live runs no checks, and an empty object would read as
        // "checked everything, found nothing wrong" rather than "checked nothing".
        Assert.False(json.TryGetProperty("checks", out _));
    }

    [Fact]
    public async Task Ready_returns_one_object_per_dependency_keyed_by_name()
    {
        var json = await WriteAsync(
            Report(HealthStatus.Healthy,
                ("postgres", Entry(HealthStatus.Healthy, ms: 3.1)),
                ("redis", Entry(HealthStatus.Healthy, ms: 1.2)),
                ("redpanda", Entry(HealthStatus.Healthy, "3 broker(s) reachable.", ms: 8.0))),
            includeChecks: true);

        var checks = json.GetProperty("checks");

        Assert.Equal("healthy", checks.GetProperty("postgres").GetProperty("status").GetString());
        Assert.Equal(3.1, checks.GetProperty("postgres").GetProperty("durationMs").GetDouble());
        Assert.Equal("healthy", checks.GetProperty("redis").GetProperty("status").GetString());
        Assert.Equal(
            "3 broker(s) reachable.",
            checks.GetProperty("redpanda").GetProperty("description").GetString());
    }

    [Fact]
    public async Task Ready_reports_the_failing_dependency_by_name()
    {
        var json = await WriteAsync(
            Report(HealthStatus.Unhealthy,
                ("postgres", Entry(HealthStatus.Healthy)),
                ("redis", Entry(HealthStatus.Unhealthy, "Redis connectivity check failed.",
                    new InvalidOperationException("host=redis-prod-7;password=hunter2")))),
            includeChecks: true);

        var checks = json.GetProperty("checks");

        Assert.Equal("healthy", checks.GetProperty("postgres").GetProperty("status").GetString());
        Assert.Equal("unhealthy", checks.GetProperty("redis").GetProperty("status").GetString());
        Assert.Equal("unhealthy", json.GetProperty("status").GetString());
    }

    /// <summary>
    /// The reason <c>error</c> is a type name. These endpoints are unauthenticated and reachable
    /// by anything on the mesh, and a driver exception's message carries the connection it failed
    /// on — host, database, sometimes credentials.
    /// </summary>
    [Fact]
    public async Task Ready_never_publishes_an_exception_message()
    {
        var json = await WriteAsync(
            Report(HealthStatus.Unhealthy,
                ("postgres", Entry(HealthStatus.Unhealthy, "Postgres connectivity check failed.",
                    new InvalidOperationException("Host=db.internal;Username=tiki;Password=s3cret")))),
            includeChecks: true);

        var postgres = json.GetProperty("checks").GetProperty("postgres");

        Assert.Equal("InvalidOperationException", postgres.GetProperty("error").GetString());
        Assert.DoesNotContain("s3cret", json.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("db.internal", json.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_healthy_check_carries_no_error_property()
    {
        var json = await WriteAsync(
            Report(HealthStatus.Healthy, ("redis", Entry(HealthStatus.Healthy))),
            includeChecks: true);

        Assert.False(json.GetProperty("checks").GetProperty("redis").TryGetProperty("error", out _));
    }

    [Fact]
    public async Task Degraded_is_reported_as_its_own_status()
    {
        var json = await WriteAsync(
            Report(HealthStatus.Degraded, ("redpanda", Entry(HealthStatus.Degraded, "1 of 3 brokers reachable."))),
            includeChecks: true);

        Assert.Equal("degraded", json.GetProperty("status").GetString());
        Assert.Equal("degraded", json.GetProperty("checks").GetProperty("redpanda").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Service_is_omitted_rather_than_null_when_it_is_not_configured()
    {
        var json = await WriteAsync(Report(HealthStatus.Healthy), includeChecks: false, serviceName: null);

        Assert.False(json.TryGetProperty("service", out _));
    }

    [Fact]
    public async Task Response_is_json()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthCheckExtensions.WriteAsync(context, Report(HealthStatus.Healthy), "wallet-service", false);

        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
    }
}
