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
    /// Applies the same JSON conventions to MVC controllers.
    /// </summary>
    /// <remarks>
    /// <see cref="AddTikiCore"/> configures <c>Http.Json.JsonOptions</c>, which minimal APIs
    /// use. Controllers read a different options type entirely, so without this call a
    /// service that uses controllers silently serialises in PascalCase with numeric enums
    /// while its minimal-API endpoints use camelCase and strings. Every service that hit this
    /// had hand-copied the same ten lines into <c>Program.cs</c>.
    /// </remarks>
    public static IServiceCollection AddTikiControllerJson(this IServiceCollection services)
    {
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(json =>
        {
            json.JsonSerializerOptions.PropertyNamingPolicy = TikiJson.Options.PropertyNamingPolicy;
            json.JsonSerializerOptions.DefaultIgnoreCondition = TikiJson.Options.DefaultIgnoreCondition;
            foreach (var converter in TikiJson.Options.Converters)
                json.JsonSerializerOptions.Converters.Add(converter);
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

    /// <summary>
    /// Adds inbound service-to-service signature verification to the pipeline. Place it
    /// after <c>UseAuthentication()</c> and before <c>UseAuthorization()</c>: it needs the
    /// endpoint to have been resolved (to see <c>[RequireServiceToken]</c>), and it must run
    /// before any authorization policy reads the identity it establishes.
    /// </summary>
    public static IApplicationBuilder UseTikiServiceAuth(this IApplicationBuilder app)
    {
        app.UseMiddleware<Auth.ServiceRequestAuthenticationMiddleware>();
        return app;
    }
}
