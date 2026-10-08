using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tiki.Shared.Messaging.InProcess;

namespace Tiki.Shared.Messaging;

/// <summary>Options for <see cref="MessagingExtensions.AddTikiMessaging"/>, bound from <c>IConfiguration</c>.</summary>
public sealed class TikiMessagingOptions
{
    public const string SectionName = "Tiki:Messaging";

    /// <summary>Comma-separated Redpanda bootstrap servers, e.g. <c>redpanda:9092</c>.</summary>
    public required string BootstrapServers { get; init; }

    /// <summary>Default consumer group id for this service — every consumer belongs to the same service, so one default is enough.</summary>
    public string? ConsumerGroupId { get; init; }

    /// <summary>
    /// How long a publish may wait for the broker to acknowledge it before it fails, in
    /// milliseconds. librdkafka's own default is five minutes, which let one unreachable broker
    /// hold a request (a payment-code email, say) open for that long. Fifteen seconds is well
    /// past a healthy round trip and short enough for the caller to answer.
    /// </summary>
    public int MessageTimeoutMs { get; init; } = 15_000;
}

public static class MessagingExtensions
{
    /// <summary>
    /// Registers <see cref="ITikiMessageProducer"/>, <see cref="RetryDlqRouter"/>, and the
    /// in-process <see cref="IEventBus"/> against Redpanda via Confluent.Kafka directly —
    /// no messaging framework in between. Register each
    /// <see cref="TikiConsumerBackgroundService{TMessage}"/> subclass as its own hosted
    /// service, one per topic, using <see cref="BuildTikiConsumer"/> to construct its consumer.
    /// </summary>
    public static IServiceCollection AddTikiMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(TikiMessagingOptions.SectionName).Get<TikiMessagingOptions>()
            ?? throw new InvalidOperationException(
                $"Missing configuration section '{TikiMessagingOptions.SectionName}' — 'BootstrapServers' is required.");

        services.AddSingleton(options);

        services.AddSingleton<IProducer<string, string>>(_ =>
            new ProducerBuilder<string, string>(options.BuildProducerConfig()).Build());

        services.AddSingleton<ITikiMessageProducer, KafkaMessageProducer>();

        services.Configure<RetryDlqRouterOptions>(configuration.GetSection(RetryDlqRouterOptions.SectionName));
        services.AddSingleton<RetryDlqRouter>();

        services.AddScoped<IEventBus, EventBus>();

        return services;
    }

    /// <summary>The producer settings every service publishes with — the broker list and a bounded delivery timeout.</summary>
    public static ProducerConfig BuildProducerConfig(this TikiMessagingOptions options) => new()
    {
        BootstrapServers = options.BootstrapServers,
        MessageTimeoutMs = options.MessageTimeoutMs > 0
            ? options.MessageTimeoutMs
            : throw new InvalidOperationException(
                $"'{TikiMessagingOptions.SectionName}:MessageTimeoutMs' must be a positive number of milliseconds."),
    };

    /// <summary>Builds a consumer configured for this service's consumer group — pass it to a <see cref="TikiConsumerBackgroundService{TMessage}"/> subclass.</summary>
    public static IConsumer<string, string> BuildTikiConsumer(this TikiMessagingOptions options, string? groupIdOverride = null) =>
        new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = groupIdOverride ?? options.ConsumerGroupId
                ?? throw new InvalidOperationException(
                    "A consumer group id is required — set 'Tiki:Messaging:ConsumerGroupId' or pass groupIdOverride."),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).Build();
}
