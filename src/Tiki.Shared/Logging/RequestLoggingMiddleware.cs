using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Auth;

namespace Tiki.Shared.Logging;

/// <summary>
/// Logs exactly one structured line per inbound request, on completion: HTTP method,
/// path, status code, duration in milliseconds, the resolved client IPv4
/// (<see cref="ClientIpAccessor"/>), tenant id, session id, calling-service/caller
/// identity if available, and trace id. Registered early in the pipeline — before auth —
/// so even a rejected request gets logged. Also mints <see cref="ServiceContext.SessionId"/>
/// for the request, so every outbound call this request goes on to make shares one
/// correlatable session id.
///
/// <para>
/// This middleware deliberately establishes <b>no</b> security context. It used to populate
/// <see cref="ServiceContext.TenantId"/> from the inbound <c>X-Tenant-Id</c> header, which
/// was a cross-tenant data-access hole: it runs before authentication, so the value came
/// straight from the caller — and <see cref="ServiceContext.TenantId"/> is what drives the
/// EF Core global tenant query filter in every service. Any client could have sent the
/// header and read another tenant's rows.
/// </para>
///
/// <para>
/// Tenant is now set in exactly two places, both of them after the value has been proven:
/// <c>AddTikiJwtAuth</c>'s token-validated handler (from the live session) and
/// <see cref="Auth.ServiceRequestAuthenticationMiddleware"/> (once the HMAC signature
/// covering the header has verified). The header is still read here, but only to log it as
/// <c>UnverifiedTenantIdHeader</c> — a diagnostic, never an input to an access decision.
/// </para>
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public const string TenantHeaderName = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        ServiceContext.SessionId = Guid.NewGuid();

        // Read for diagnostics only — see the class remarks. Assigning this to
        // ServiceContext.TenantId here would trust an unauthenticated caller's own claim
        // about which tenant's data to return.
        var unverifiedTenantHeader = context.Request.Headers[TenantHeaderName].FirstOrDefault();

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
                "{Method} {Path} responded {StatusCode} in {ElapsedMs}ms — client {ClientIp}, tenant {TenantId}, session {SessionId}, caller {CallingService}, trace {TraceId}, unverifiedTenantHeader {UnverifiedTenantIdHeader}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                clientIp ?? "unknown",
                ServiceContext.TenantId,
                ServiceContext.SessionId,
                ServiceContext.CallingService ?? "unknown",
                ServiceContext.TraceId,
                unverifiedTenantHeader ?? "none");
        }
    }
}
