using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Tiki.Shared.Core.Middleware;

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
    /// Wires <see cref="CorrelationIdMiddleware"/> and <see cref="ErrorHandlingMiddleware"/>
    /// in the correct order — correlation id first, so a subsequent error is logged and
    /// returned with the same trace id every other span in the request carries.
    /// </summary>
    public static IApplicationBuilder UseTikiCore(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ErrorHandlingMiddleware>();
        return app;
    }
}
