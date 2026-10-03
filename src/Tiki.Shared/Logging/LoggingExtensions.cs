using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;
using Tiki.Shared.Telemetry;

namespace Tiki.Shared.Logging;

public static class LoggingExtensions
{
    /// <summary>The text-format template. Alloy recognises the <c>[HH:mm:ss LVL]</c> prefix as the start of an event.</summary>
    public const string TextOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Applies every Tiki logging convention to a Serilog <see cref="LoggerConfiguration"/>
    /// in one call: <see cref="TikiLogEnrichers"/> (trace/span id, service name, calling
    /// service, tenant/user/session id), <c>Enrich.FromLogContext()</c> (so
    /// <c>ILogger.BeginScope</c> properties reach the line), and
    /// <see cref="SensitiveDataMaskingPolicy"/> (masks every <c>[Sensitive]</c>-attributed
    /// property on any destructured object, for every sink). Adds no sink — see the overload
    /// taking <see cref="IConfiguration"/> for the standard console sink.
    /// </summary>
    public static LoggerConfiguration ConfigureTikiLogging(this LoggerConfiguration configuration, string serviceName) =>
        configuration
            .Enrich.FromLogContext()
            .Enrich.With(new TikiLogEnrichers(serviceName))
            .Destructure.With<SensitiveDataMaskingPolicy>();

    /// <summary>
    /// <see cref="ConfigureTikiLogging(LoggerConfiguration, string)"/> plus the standard sinks:
    /// the console, as JSON or text per <see cref="ResolveFormat"/>, and — only when
    /// <c>Tiki:Logging:Otlp:Enabled</c> is true — OTLP to the collector. Replaces a
    /// service's own <c>.WriteTo.Console()</c>; calling both writes every line twice.
    /// </summary>
    /// <param name="configuration">The logger being built.</param>
    /// <param name="serviceName">The service's OpenTelemetry <c>service.name</c>.</param>
    /// <param name="appConfiguration">The host's configuration (<c>context.Configuration</c>).</param>
    /// <param name="environmentName">The host environment (<c>context.HostingEnvironment.EnvironmentName</c>); read from <c>ASPNETCORE_ENVIRONMENT</c>/<c>DOTNET_ENVIRONMENT</c> when omitted.</param>
    public static LoggerConfiguration ConfigureTikiLogging(
        this LoggerConfiguration configuration,
        string serviceName,
        IConfiguration appConfiguration,
        string? environmentName = null)
    {
        ArgumentNullException.ThrowIfNull(appConfiguration);

        configuration.ConfigureTikiLogging(serviceName);

        if (ResolveFormat(appConfiguration, environmentName) == TikiLogFormat.Json)
            configuration.WriteTo.Console(new RenderedCompactJsonFormatter());
        else
            configuration.WriteTo.Console(outputTemplate: TextOutputTemplate);

        var options = TikiLoggingOptions.Bind(appConfiguration);
        var endpoint = options.Otlp.Endpoint
            ?? appConfiguration.GetSection(TikiTelemetryOptions.SectionName).Get<TikiTelemetryOptions>()?.OtlpEndpoint;

        if (options.Otlp.Enabled && !string.IsNullOrWhiteSpace(endpoint))
        {
            configuration.WriteTo.OpenTelemetry(otlp =>
            {
                otlp.Endpoint = endpoint;
                otlp.Protocol = OtlpProtocol.Grpc;
                otlp.ResourceAttributes = new Dictionary<string, object> { ["service.name"] = serviceName };
            });
        }

        return configuration;
    }

    /// <summary>
    /// Which console format to write:
    /// <list type="number">
    ///   <item><c>Tiki:Logging:Format</c> (<c>json</c> | <c>text</c>) when set — the explicit override.</item>
    ///   <item><see cref="TikiLogFormat.Json"/> inside a container (<c>DOTNET_RUNNING_IN_CONTAINER=true</c>, which every
    ///   <c>mcr.microsoft.com/dotnet</c> image sets) — whatever the environment, so the local docker stack and EKS
    ///   both feed Alloy structured lines.</item>
    ///   <item><see cref="TikiLogFormat.Text"/> for <c>Development</c> outside a container — <c>dotnet run</c> on a laptop, read by a person.</item>
    ///   <item><see cref="TikiLogFormat.Json"/> otherwise — any other non-container host is a machine reading the output.</item>
    /// </list>
    /// </summary>
    public static TikiLogFormat ResolveFormat(IConfiguration configuration, string? environmentName = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var explicitFormat = TikiLoggingOptions.Bind(configuration).Format;
        if (string.Equals(explicitFormat, "json", StringComparison.OrdinalIgnoreCase))
            return TikiLogFormat.Json;
        if (string.Equals(explicitFormat, "text", StringComparison.OrdinalIgnoreCase))
            return TikiLogFormat.Text;

        var inContainer = configuration["DOTNET_RUNNING_IN_CONTAINER"]
            ?? Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        if (string.Equals(inContainer, "true", StringComparison.OrdinalIgnoreCase))
            return TikiLogFormat.Json;

        var environment = environmentName
            ?? configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["DOTNET_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";

        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)
            ? TikiLogFormat.Text
            : TikiLogFormat.Json;
    }
}
