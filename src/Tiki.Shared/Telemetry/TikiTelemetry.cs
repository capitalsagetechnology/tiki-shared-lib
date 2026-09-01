using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Tiki.Shared.Telemetry;

/// <summary>
/// The shared <see cref="ActivitySource"/> and <see cref="Meter"/> every Tiki service
/// instruments its Kafka publish/consume spans with. Vendor-neutral OpenTelemetry only —
/// no proprietary tracer type is exposed to consuming services.
/// </summary>
public static class TikiTelemetry
{
    public const string SourceName = "Tiki.Shared.Messaging";

    /// <summary>Used by <see cref="Messaging.KafkaMessageProducer"/> and <see cref="Messaging.TikiConsumerBackgroundService{TMessage}"/>
    /// to start publish/consume spans that continue the caller's trace.</summary>
    public static readonly ActivitySource MessagingSource = new(SourceName, ThisAssemblyVersion);

    /// <summary>Shared meter for counters/histograms emitted by this package (cache hit rate, retry counts, etc.).</summary>
    public static readonly Meter Meter = new(SourceName, ThisAssemblyVersion);

    private static string ThisAssemblyVersion =>
        typeof(TikiTelemetry).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}
