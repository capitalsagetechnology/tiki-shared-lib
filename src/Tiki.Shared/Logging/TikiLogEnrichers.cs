using Serilog.Core;
using Serilog.Events;
using Tiki.Shared.Auth;

namespace Tiki.Shared.Logging;

/// <summary>
/// Attaches trace id, service name, and calling-service id to every log line automatically
/// — a log line from any service, searched by trace id, is findable without knowing that
/// service's log format in advance. Never enriches with a secret, token, or credential
/// value: only these three fields are ever added.
/// </summary>
/// <remarks>Register with Serilog directly: <c>.Enrich.With(new TikiLogEnrichers("wallet-service"))</c>.</remarks>
public sealed class TikiLogEnrichers(string serviceName) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", ServiceContext.TraceId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ServiceName", serviceName));

        if (ServiceContext.CallingService is { } callingService)
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CallingService", callingService));
    }
}
