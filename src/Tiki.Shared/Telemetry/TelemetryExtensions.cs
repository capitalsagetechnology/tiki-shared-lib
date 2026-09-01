using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Tiki.Shared.Telemetry;

/// <summary>Options bound from <c>IConfiguration</c> for <see cref="TelemetryExtensions.AddTikiTelemetry"/>.</summary>
public sealed class TikiTelemetryOptions
{
    public const string SectionName = "Tiki:Telemetry";

    /// <summary>OTLP endpoint for the shared Collector, e.g. <c>http://otel-collector:4317</c>.</summary>
    public string? OtlpEndpoint { get; init; }

    /// <summary>Fraction of traces sampled when no parent decision is already present (1.0 = always).</summary>
    public double SamplingRatio { get; init; } = 1.0;
}

public static class TelemetryExtensions
{
    /// <summary>
    /// Wires OpenTelemetry tracing (ASP.NET Core, gRPC client, HTTP client, and this
    /// package's own Kafka producer/consumer <see cref="TikiTelemetry.MessagingSource"/>)
    /// and metrics, exporting via OTLP to the shared Collector. One call in
    /// <c>Program.cs</c> produces a trace spanning an inbound HTTP request, an outbound
    /// gRPC call, and a Kafka publish, all under one trace id.
    /// </summary>
    public static IServiceCollection AddTikiTelemetry(
        this IServiceCollection services, string serviceName, IConfiguration configuration)
    {
        var options = configuration.GetSection(TikiTelemetryOptions.SectionName).Get<TikiTelemetryOptions>()
            ?? new TikiTelemetryOptions();

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new TraceIdRatioBasedSampler(options.SamplingRatio))
                    .AddSource(TikiTelemetry.SourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(TikiTelemetry.SourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
                }
            });

        return services;
    }
}
