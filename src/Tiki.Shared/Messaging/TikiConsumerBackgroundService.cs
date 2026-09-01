using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Auth;
using Tiki.Shared.Core.Events;
using Tiki.Shared.Extensions;
using Tiki.Shared.Telemetry;

namespace Tiki.Shared.Messaging;

/// <summary>
/// Base class for a Kafka consumer. Delivery is at-least-once: a handler failure routes
/// the message to <c>{topic}.retry</c> via <see cref="RetryDlqRouter"/> rather than
/// crashing the loop, so <see cref="HandleAsync"/> can be re-invoked for the same message
/// — it MUST be idempotent.
/// </summary>
public abstract class TikiConsumerBackgroundService<TMessage>(
    IConsumer<string, string> consumer,
    RetryDlqRouter retryDlqRouter,
    string topic,
    ILogger logger) : BackgroundService
    where TMessage : BaseEvent
{
    protected abstract Task HandleAsync(TMessage message, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        consumer.Subscribe(topic);

        while (!ct.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result;
            try
            {
                result = consumer.Consume(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Consume failed for {Topic}", topic);
                continue;
            }

            if (result?.Message is null)
                continue;

            var traceParent = ExtractTraceParent(result.Message.Headers);
            using var activity = TikiTelemetry.MessagingSource.StartActivity(
                $"consume {topic}", ActivityKind.Consumer, parentId: traceParent ?? string.Empty);

            using var _ = ServiceContext.BeginScope(
                activity?.TraceId.ToString() ?? traceParent ?? Guid.NewGuid().ToString("n"), callingService: null);

            try
            {
                var message = JsonSerializer.Deserialize<TMessage>(result.Message.Value, TikiJson.Options)
                    ?? throw new JsonException($"Deserializing {typeof(TMessage).Name} from topic '{topic}' produced null.");

                await HandleAsync(message, ct);
                consumer.Commit(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Handler failed for {Topic}, routing to retry topic", topic);
                await retryDlqRouter.RouteToRetryAsync(topic, result.Message, ex, ct);
                consumer.Commit(result);
            }
        }
    }

    private static string? ExtractTraceParent(Headers headers) =>
        headers.TryGetLastBytes("traceparent", out var bytes) ? Encoding.UTF8.GetString(bytes) : null;

    public override void Dispose()
    {
        consumer.Close();
        consumer.Dispose();
        base.Dispose();
    }
}
