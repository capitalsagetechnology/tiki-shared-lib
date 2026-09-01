using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Auth;

namespace Tiki.Shared.Logging;

/// <summary>
/// Logs exactly one structured line per inbound request, on completion: HTTP method,
/// path, status code, duration in milliseconds, the resolved client IPv4
/// (<see cref="ClientIpAccessor"/>), tenant id, calling-service/caller identity if
/// available, and trace id. Registered early in the pipeline — before auth — so even a
/// rejected request gets logged.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public const string TenantHeaderName = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        if (Guid.TryParse(context.Request.Headers[TenantHeaderName].FirstOrDefault(), out var tenantId))
            ServiceContext.TenantId = tenantId;

        var clientIp = ClientIpAccessor.Resolve(context);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            logger.LogInformation(
                "{Method} {Path} responded {StatusCode} in {ElapsedMs}ms — client {ClientIp}, tenant {TenantId}, caller {CallingService}, trace {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                clientIp ?? "unknown",
                ServiceContext.TenantId,
                ServiceContext.CallingService ?? "unknown",
                ServiceContext.TraceId);
        }
    }
}
