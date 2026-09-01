using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tiki.Shared.Messaging;

/// <summary>Options for <see cref="RetryDlqRouter"/>, bound from <c>IConfiguration</c>.</summary>
public sealed class RetryDlqRouterOptions
{
    public const string SectionName = "Tiki:Messaging:RetryDlq";

    /// <summary>How many times a message is redelivered via <c>{topic}.retry</c> before it moves to <c>{topic}.dlq</c>.</summary>
    public int MaxRetryAttempts { get; init; } = 3;
}

/// <summary>
/// Implements the <c>{topic}.retry</c> / <c>{topic}.dlq</c> pattern once, reusably, for
/// every <see cref="TikiConsumerBackgroundService{TMessage}"/>. A handler failure routes
/// the message to <c>{topic}.retry</c> with an incremented attempt-count header; once
/// <see cref="RetryDlqRouterOptions.MaxRetryAttempts"/> is exceeded, it routes to
/// <c>{topic}.dlq</c> instead, where it needs human attention.
/// </summary>
public sealed class RetryDlqRouter(
    IProducer<string, string> producer,
    IOptions<RetryDlqRouterOptions> options,
    ILogger<RetryDlqRouter> logger)
{
    private const string RetryCountHeader = "x-retry-count";
    private readonly RetryDlqRouterOptions _options = options.Value;

    public async Task RouteToRetryAsync(string topic, Message<string, string> original, Exception failure, CancellationToken ct = default)
    {
        var attempt = GetRetryCount(original.Headers) + 1;
        var targetTopic = attempt <= _options.MaxRetryAttempts ? $"{topic}.retry" : $"{topic}.dlq";

        var headers = new Headers();
        foreach (var header in original.Headers)
        {
            if (header.Key != RetryCountHeader)
                headers.Add(header.Key, header.GetValueBytes());
        }

        headers.Add(RetryCountHeader, Encoding.UTF8.GetBytes(attempt.ToString()));
        headers.Add("x-failure-reason", Encoding.UTF8.GetBytes(failure.Message));

        logger.LogWarning(
            "Routing message from {Topic} to {TargetTopic} (attempt {Attempt}/{MaxAttempts})",
            topic, targetTopic, attempt, _options.MaxRetryAttempts);

        await producer.ProduceAsync(targetTopic, new Message<string, string>
        {
            Key = original.Key,
            Value = original.Value,
            Headers = headers,
        }, ct);
    }

    private static int GetRetryCount(Headers headers)
    {
        if (!headers.TryGetLastBytes(RetryCountHeader, out var bytes))
            return 0;

        return int.TryParse(Encoding.UTF8.GetString(bytes), out var count) ? count : 0;
    }
}
