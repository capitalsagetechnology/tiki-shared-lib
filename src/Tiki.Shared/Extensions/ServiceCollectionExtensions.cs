using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Tiki.Shared.Core.Middleware;
using Tiki.Shared.Logging;

namespace Tiki.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the baseline every Tiki service needs regardless of which other modules
    /// it opts into: <see cref="TikiJson"/> as the ASP.NET Core JSON convention.
    /// </summary>
    public static IServiceCollection AddTikiCore(this IServiceCollection services)
    {
        services.Configure<JsonOptions>(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = TikiJson.Options.PropertyNamingPolicy;
            json.SerializerOptions.DefaultIgnoreCondition = TikiJson.Options.DefaultIgnoreCondition;
            foreach (var converter in TikiJson.Options.Converters)
                json.SerializerOptions.Converters.Add(converter);
        });

        return services;
    }

    /// <summary>
    /// Wires <see cref="CorrelationIdMiddleware"/>, <see cref="RequestLoggingMiddleware"/>,
    /// and <see cref="ErrorHandlingMiddleware"/> in the correct order: correlation id first
    /// so the trace id is already set before anything logs it; request logging wraps error
    /// handling (not the other way round) so its one log line per request reports whatever
    /// status code error handling ultimately produced; both run before authentication, so
    /// even a rejected request is logged with the right trace id.
    /// </summary>
    public static IApplicationBuilder UseTikiCore(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<ErrorHandlingMiddleware>();
        return app;
    }
}
