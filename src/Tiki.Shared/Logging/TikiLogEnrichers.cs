using Serilog.Core;
using Serilog.Events;
using Tiki.Shared.Auth;
using Tiki.Shared.Telemetry;

namespace Tiki.Shared.Logging;

/// <summary>
/// Attaches the correlation and identity ids to every log line automatically — a log line from
/// any service, searched by trace id or tenant id, is findable without knowing that service's
/// log format in advance.
/// </summary>
/// <remarks>
/// <para>Added, when present:</para>
/// <list type="bullet">
///   <item><c>TraceId</c> / <c>SpanId</c> — Tempo's 32-hex/16-hex ids (<see cref="TikiTraceIds"/>), on HTTP and Kafka paths alike.</item>
///   <item><c>ServiceName</c>, <c>CallingService</c>.</item>
///   <item><c>TenantId</c>, <c>UserId</c>, <c>SessionId</c> from <see cref="ServiceContext"/>.</item>
/// </list>
/// <para>
/// Ids only: never a name, email, phone number, secret, token or credential. A property the log
/// call already supplies (e.g. <c>TenantId</c> on the request line) is left as the caller wrote it.
/// </para>
/// <para>
/// There is no ambient business id: a session can hold grants for several businesses and none
/// is selected on <see cref="ServiceContext"/>, so a <c>BusinessId</c> appears only where the
/// log call names it.
/// </para>
/// <para>Register with Serilog directly: <c>.Enrich.With(new TikiLogEnrichers("wallet-service"))</c>, or via <see cref="LoggingExtensions.ConfigureTikiLogging(Serilog.LoggerConfiguration, string)"/>.</para>
/// </remarks>
public sealed class TikiLogEnrichers(string serviceName) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (TikiTraceIds.CurrentTraceId() is { } traceId)
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId));
        if (TikiTraceIds.CurrentSpanId() is { } spanId)
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", spanId));

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ServiceName", serviceName));

        if (ServiceContext.CallingService is { } callingService)
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CallingService", callingService));
        if (ServiceContext.TenantId is { } tenantId)
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TenantId", tenantId));
        if (ServiceContext.UserId is { } userId)
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", userId));
        if (ServiceContext.SessionId is { } sessionId)
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SessionId", sessionId));
    }
}
