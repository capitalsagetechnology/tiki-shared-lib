using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Tiki.Shared.Auth;

namespace Tiki.Shared.Core.Middleware;

/// <summary>
/// Reads the inbound trace id (from the current <see cref="Activity"/> if ASP.NET Core's
/// W3C trace-context propagation already started one, else the <c>X-Correlation-Id</c>
/// header) or generates one, and makes it available via <see cref="ServiceContext"/> for
/// the lifetime of the request. Every log line and every outbound gRPC/Kafka call made
/// during the request should read it from there rather than re-deriving it.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Activity.Current?.Id
            ?? context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("n");

        // Advisory only at this point in the pipeline: the signature that makes this header
        // trustworthy has not been checked yet (that is
        // ServiceRequestAuthenticationMiddleware, further down). It is used for log
        // correlation, never for an authorization decision — the verified value overwrites
        // this one once the request is authenticated.
        var callingService = context.Request.Headers[Gateway.TikiHeaderNames.ServiceId].FirstOrDefault();

        using (ServiceContext.BeginScope(traceId, callingService))
        {
            context.Response.Headers[HeaderName] = traceId;
            await next(context);
        }
    }
}
