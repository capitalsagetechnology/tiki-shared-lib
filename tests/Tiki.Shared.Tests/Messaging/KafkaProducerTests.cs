using Confluent.Kafka;
using Moq;
using Tiki.Shared.Core.Events;
using Tiki.Shared.Messaging;
using Xunit;

namespace Tiki.Shared.Tests.Messaging;

file sealed record TestEvent : BaseEvent
{
    public required string Payload { get; init; }
}

public class KafkaProducerTests
{
    [Fact]
    public async Task PublishAsync_sends_the_partition_key_as_the_message_key()
    {
        Message<string, string>? captured = null;
        var producer = new Mock<IProducer<string, string>>();
        producer
            .Setup(p => p.ProduceAsync("wallet.balance-updated", It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((_, message, _) => captured = message)
            .ReturnsAsync(new DeliveryResult<string, string>());

        var sut = new KafkaMessageProducer(producer.Object);
        var @event = new TestEvent { TraceId = "trace-1", SourceService = "wallet-service", SubjectId = "sub-1", Payload = "hello" };

        await sut.PublishAsync("wallet.balance-updated", "sub-account-42", @event);

        Assert.NotNull(captured);
        Assert.Equal("sub-account-42", captured!.Key);
        Assert.Contains("hello", captured.Value);
    }

    [Fact]
    public async Task PublishAsync_attaches_a_traceparent_header()
    {
        Message<string, string>? captured = null;
        var producer = new Mock<IProducer<string, string>>();
        producer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((_, message, _) => captured = message)
            .ReturnsAsync(new DeliveryResult<string, string>());

        var sut = new KafkaMessageProducer(producer.Object);
        var @event = new TestEvent { TraceId = "trace-1", SourceService = "wallet-service", SubjectId = "sub-1", Payload = "hello" };

        await sut.PublishAsync("wallet.balance-updated", "sub-account-42", @event);

        Assert.True(captured!.Headers.TryGetLastBytes("traceparent", out _));
    }

    [Fact]
    public async Task PublishAsync_rejects_a_missing_partition_key()
    {
        var producer = new Mock<IProducer<string, string>>();
        var sut = new KafkaMessageProducer(producer.Object);
        var @event = new TestEvent { TraceId = "trace-1", SourceService = "wallet-service", SubjectId = "sub-1", Payload = "hello" };

        await Assert.ThrowsAsync<ArgumentException>(() => sut.PublishAsync("topic", "", @event));
    }
}
