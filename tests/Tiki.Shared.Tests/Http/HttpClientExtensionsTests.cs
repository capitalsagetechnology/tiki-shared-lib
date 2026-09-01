using Microsoft.Extensions.DependencyInjection;
using Tiki.Shared.Http;
using Xunit;

namespace Tiki.Shared.Tests.Http;

public class HttpClientExtensionsTests
{
    [Fact]
    public void AddTikiExternalHttpClient_wires_the_session_lifecycle_handler_into_every_client_built_through_it()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTikiExternalHttpClient("compliance");

        using var provider = services.BuildServiceProvider();
        var handlerFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();
        var handler = handlerFactory.CreateHandler("compliance");

        Assert.True(ChainContains<SessionLifecycleLoggingHandler>(handler));
    }

    private static bool ChainContains<THandler>(HttpMessageHandler handler) where THandler : DelegatingHandler
    {
        var current = handler;
        while (current is DelegatingHandler delegating)
        {
            if (delegating is THandler)
                return true;

            current = delegating.InnerHandler;
        }

        return false;
    }
}
