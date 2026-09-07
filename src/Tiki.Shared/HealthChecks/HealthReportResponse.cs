using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Tiki.Shared.HealthChecks;

/// <summary>
/// The body <c>/health/live</c> and <c>/health/ready</c> return.
/// </summary>
/// <remarks>
/// Both endpoints previously returned the ASP.NET default: the bare string <c>Healthy</c> as
/// <c>text/plain</c>. That is enough for a container healthcheck, which only reads the status
/// code, and not enough for anything else — a failing <c>/health/ready</c> said only that
/// <em>something</em> was unreachable, so working out whether it was Postgres, Redis or Redpanda
/// meant going to the logs of the pod that was already refusing traffic.
/// </remarks>
public sealed record HealthReportResponse
{
    /// <summary><c>healthy</c>, <c>degraded</c> or <c>unhealthy</c> — the aggregate.</summary>
    [JsonConverter(typeof(HealthStatusConverter))]
    public required HealthStatus Status { get; init; }

    /// <summary>Which service answered. A body copied into a ticket is otherwise unattributable.</summary>
    public string? Service { get; init; }

    public required DateTimeOffset CheckedAt { get; init; }

    /// <summary>Wall-clock time for the whole report, so a slow dependency is visible before it fails outright.</summary>
    public required double TotalDurationMs { get; init; }

    /// <summary>
    /// One entry per registered check, keyed by name — <c>postgres</c>, <c>redis</c>,
    /// <c>redpanda</c>. Absent on <c>/health/live</c>, which runs no checks at all.
    /// </summary>
    public IReadOnlyDictionary<string, HealthCheckEntry>? Checks { get; init; }
}

/// <summary>One dependency's result.</summary>
public sealed record HealthCheckEntry
{
    [JsonConverter(typeof(HealthStatusConverter))]
    public required HealthStatus Status { get; init; }

    /// <summary>
    /// The check's own description — <c>"Postgres connectivity check failed."</c>,
    /// <c>"3 broker(s) reachable."</c> Written by the check, so it is safe to publish.
    /// </summary>
    public string? Description { get; init; }

    public required double DurationMs { get; init; }

    /// <summary>
    /// The exception's <em>type name</em> when a check faulted, never its message.
    /// </summary>
    /// <remarks>
    /// The distinction is the point. A driver exception carries the connection it failed on —
    /// host, database, sometimes the user — and these endpoints are unauthenticated and reachable
    /// by anything on the mesh. <c>NpgsqlException</c> tells an operator which layer broke;
    /// the message would tell a reader the topology.
    /// </remarks>
    public string? Error { get; init; }
}

/// <summary>
/// Emits <see cref="HealthStatus"/> as <c>healthy</c> / <c>degraded</c> / <c>unhealthy</c>.
/// </summary>
/// <remarks>
/// Named rather than inline because a property-level <c>[JsonConverter]</c> is constructed with
/// its parameterless constructor, so the bare <c>JsonStringEnumConverter&lt;HealthStatus&gt;</c>
/// would ignore the camelCase policy on <c>TikiJson.Options</c> and emit <c>Healthy</c>. Pinning
/// it here keeps the wire shape the same whichever options serialise the record.
/// </remarks>
internal sealed class HealthStatusConverter()
    : JsonStringEnumConverter<HealthStatus>(JsonNamingPolicy.CamelCase);
