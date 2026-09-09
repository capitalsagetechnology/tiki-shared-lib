using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Tiki.Shared.Auth;
using Xunit;

namespace Tiki.Shared.Tests.Auth;

/// <summary>
/// A service must come up, and stay up, while Redis is unreachable.
/// </summary>
/// <remarks>
/// StackExchange's default is to throw from Connect() when the first attempt fails. Inside a
/// DI factory that throw repeats on every resolution — ten seconds per request — and it reached
/// the liveness probe, so a service that merely started before Redis was restarted in a loop.
/// The multiplexer has to be created regardless and left to reconnect on its own.
/// </remarks>
public class RedisAvailabilityTests
{
    [Fact]
    public void The_multiplexer_is_created_even_when_redis_is_unreachable()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // A port nothing listens on, and a short timeout so the test does not wait.
                ["Tiki:Caching:RedisConnectionString"] = "127.0.0.1:1,connectTimeout=200,connectRetry=1",
                ["Tiki:Auth:ServiceIdentity:ServiceId"] = "test-service",
                ["Tiki:Auth:ServiceIdentity:SigningSecret"] = new string('s', 48),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTikiServiceAuth(configuration);

        using var provider = services.BuildServiceProvider();

        // The thing that used to throw. Resolving must succeed; only an operation may fail.
        var multiplexer = provider.GetRequiredService<IConnectionMultiplexer>();

        Assert.NotNull(multiplexer);
        Assert.False(multiplexer.IsConnected);

        // And the verifier — what every signed request resolves — must be constructible too.
        Assert.NotNull(provider.GetRequiredService<IServiceRequestVerifier>());
    }
}
