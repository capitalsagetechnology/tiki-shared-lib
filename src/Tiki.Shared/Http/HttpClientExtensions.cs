using Microsoft.Extensions.DependencyInjection;

namespace Tiki.Shared.Http;

public static class HttpClientExtensions
{
    /// <summary>
    /// Registers a named external <c>HttpClient</c> with <see cref="SessionLifecycleLoggingHandler"/>
    /// attached, so every call made through it logs its full started/completed/failed
    /// lifecycle tagged with the current session id and trace id. Chain further handlers
    /// (retry, circuit breaker, idempotency) onto the returned builder as a service needs
    /// them — <see cref="SessionLifecycleLoggingHandler"/> is added first, so it wraps
    /// everything else added afterward and times the full round trip including retries.
    /// </summary>
    public static IHttpClientBuilder AddTikiExternalHttpClient(this IServiceCollection services, string name)
    {
        services.AddTransient<SessionLifecycleLoggingHandler>();

        return services.AddHttpClient(name)
            .AddHttpMessageHandler<SessionLifecycleLoggingHandler>();
    }
}
