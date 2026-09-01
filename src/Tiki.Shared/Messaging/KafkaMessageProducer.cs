using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Tiki.Shared.Auth;
using Tiki.Shared.Core.Events;
using Tiki.Shared.Extensions;
using Tiki.Shared.Telemetry;

namespace Tiki.Shared.Messaging;

/// <summary>
/// Publishes directly to Redpanda via a Kafka-protocol client (Confluent.Kafka) — no
/// MassTransit or equivalent messaging framework in between. Every publish carries a
/// <c>traceparent</c> header so the consumer's span continues the same trace.
/// </summary>
public sealed class KafkaMessageProducer(IProducer<string, string> producer) : ITikiMessageProducer, IAsyncDisposable
{
    public async Task PublishAsync<T>(string topic, string partitionKey, T message, CancellationToken ct = default)
        where T : BaseEvent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

        using var activity = TikiTelemetry.MessagingSource.StartActivity($"publish {topic}", ActivityKind.Producer);
        var headers = new Headers
        {
            { "traceparent", Encoding.UTF8.GetBytes(activity?.Id ?? ServiceContext.TraceId) },
        };

        await producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = partitionKey,
            Value = JsonSerializer.Serialize(message, TikiJson.Options),
            Headers = headers,
        }, ct);
    }

    public ValueTask DisposeAsync()
    {
        producer.Flush(TimeSpan.FromSeconds(5));
        producer.Dispose();
        return ValueTask.CompletedTask;
    }
}
