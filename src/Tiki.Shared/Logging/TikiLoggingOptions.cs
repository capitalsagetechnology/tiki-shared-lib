using Microsoft.Extensions.Configuration;

namespace Tiki.Shared.Logging;

/// <summary>How a Tiki service writes its console logs.</summary>
public enum TikiLogFormat
{
    /// <summary>
    /// Serilog compact JSON (CLEF), one object per line, rendered message included
    /// (<see cref="TikiCompactJsonFormatter"/>). Every structured property — TenantId, TraceId,
    /// StatusCode … — survives as its own field, which is what Alloy turns into Loki structured
    /// metadata.
    /// </summary>
    Json,

    /// <summary><c>[HH:mm:ss LVL] message</c> — for a human reading a terminal during <c>dotnet run</c>.</summary>
    Text,
}

/// <summary>Options bound from <c>Tiki:Logging</c>.</summary>
public sealed class TikiLoggingOptions
{
    public const string SectionName = "Tiki:Logging";

    /// <summary>
    /// <c>json</c> or <c>text</c>. Unset (the normal case) resolves by where the process runs —
    /// see <see cref="LoggingExtensions.ResolveFormat"/>.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>Optional second sink: logs over OTLP to the collector. Off by default.</summary>
    public TikiOtlpLoggingOptions Otlp { get; init; } = new();

    internal static TikiLoggingOptions Bind(IConfiguration configuration) =>
        configuration.GetSection(SectionName).Get<TikiLoggingOptions>() ?? new TikiLoggingOptions();
}

/// <summary>Options bound from <c>Tiki:Logging:Otlp</c>.</summary>
public sealed class TikiOtlpLoggingOptions
{
    /// <summary>
    /// Off by default. Turning it on also needs the service listed in the shared infra's
    /// <c>TIKI_LOGS_OTLP_SERVICES</c>, which drops the container copy so each line is stored
    /// once. The console sink stays on either way, for crashes before the exporter starts.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>OTLP/gRPC endpoint. Unset falls back to <c>Tiki:Telemetry:OtlpEndpoint</c>, the one traces and metrics use.</summary>
    public string? Endpoint { get; init; }
}
