using Tiki.Shared.Messaging;
using Xunit;

namespace Tiki.Shared.Tests.Messaging;

public class ProducerConfigTests
{
    [Fact]
    public void A_publish_gives_up_after_fifteen_seconds_by_default_not_librdkafkas_five_minutes()
    {
        var config = new TikiMessagingOptions { BootstrapServers = "redpanda:9092" }.BuildProducerConfig();

        Assert.Equal("redpanda:9092", config.BootstrapServers);
        Assert.Equal(15_000, config.MessageTimeoutMs);
    }

    [Fact]
    public void A_service_can_set_its_own_delivery_timeout()
    {
        var config = new TikiMessagingOptions { BootstrapServers = "redpanda:9092", MessageTimeoutMs = 5_000 }
            .BuildProducerConfig();

        Assert.Equal(5_000, config.MessageTimeoutMs);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_timeout_that_is_not_positive_is_refused_at_startup(int timeoutMs)
    {
        var options = new TikiMessagingOptions { BootstrapServers = "redpanda:9092", MessageTimeoutMs = timeoutMs };

        Assert.Throws<InvalidOperationException>(() => options.BuildProducerConfig());
    }
}
