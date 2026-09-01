using Serilog;

namespace Tiki.Shared.Logging;

public static class LoggingExtensions
{
    /// <summary>
    /// Applies every Tiki logging convention to a Serilog <see cref="LoggerConfiguration"/>
    /// in one call: <see cref="TikiLogEnrichers"/> (trace id, service name, calling
    /// service) and <see cref="SensitiveDataMaskingPolicy"/> (masks every
    /// <c>[Sensitive]</c>-attributed property on any destructured object, for every sink).
    /// </summary>
    public static LoggerConfiguration ConfigureTikiLogging(this LoggerConfiguration configuration, string serviceName) =>
        configuration
            .Enrich.With(new TikiLogEnrichers(serviceName))
            .Destructure.With<SensitiveDataMaskingPolicy>();
}
