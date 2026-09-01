using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Tiki.Shared.Messaging;
using Xunit;

namespace Tiki.Shared.Tests.Messaging;

public class RetryDlqRouterTests
{
    private static RetryDlqRouter CreateSut(Mock<IProducer<string, string>> producer, int maxAttempts = 3) =>
        new(producer.Object, Options.Create(new RetryDlqRouterOptions { MaxRetryAttempts = maxAttempts }), NullLogger<RetryDlqRouter>.Instance);

    [Fact]
    public async Task First_failure_routes_to_the_retry_topic()
    {
        string? capturedTopic = null;
        var producer = new Mock<IProducer<string, string>>();
        producer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((topic, _, _) => capturedTopic = topic)
            .ReturnsAsync(new DeliveryResult<string, string>());

        var sut = CreateSut(producer);
        var original = new Message<string, string> { Key = "k", Value = "v", Headers = new Headers() };

        await sut.RouteToRetryAsync("compliance.screen-transaction-requested", original, new InvalidOperationException("boom"));

        Assert.Equal("compliance.screen-transaction-requested.retry", capturedTopic);
    }

    [Fact]
    public async Task Exceeding_max_attempts_routes_to_the_dlq_topic()
    {
        var producedTopics = new List<string>();
        var producer = new Mock<IProducer<string, string>>();
        producer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((topic, _, _) => producedTopics.Add(topic))
            .ReturnsAsync(new DeliveryResult<string, string>());

        var sut = CreateSut(producer, maxAttempts: 2);
        const string topic = "compliance.screen-transaction-requested";
        var message = new Message<string, string> { Key = "k", Value = "v", Headers = new Headers() };

        // Simulate three consecutive handler failures for the same logical message, each
        // one re-routed through {topic}.retry carrying the previous attempt's headers —
        // exactly how a redelivered retry-topic message would come back through this
        // router again after another handler failure.
        for (var i = 0; i < 3; i++)
        {
            await sut.RouteToRetryAsync(topic, message, new InvalidOperationException("boom"));
            message = new Message<string, string> { Key = message.Key, Value = message.Value, Headers = LastProducedHeaders(producer) };
        }

        Assert.Equal([$"{topic}.retry", $"{topic}.retry", $"{topic}.dlq"], producedTopics);
    }

    private static Headers LastProducedHeaders(Mock<IProducer<string, string>> producer)
    {
        var invocation = producer.Invocations.Last();
        return ((Message<string, string>)invocation.Arguments[1]).Headers;
    }
}
