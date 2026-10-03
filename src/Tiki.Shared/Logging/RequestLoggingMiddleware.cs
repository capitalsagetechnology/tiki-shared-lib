using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Auth;
using Tiki.Shared.Telemetry;

namespace Tiki.Shared.Logging;

/// <summary>
/// Logs exactly one structured line per inbound request, on completion: HTTP method,
/// path, status code, duration in milliseconds, the resolved client IPv4
/// (<see cref="ClientIpAccessor"/>), tenant id, session id, calling-service/caller
/// identity if available, trace id, and user id. Registered early in the pipeline — before
/// auth — so even a rejected request gets logged. Also mints <see cref="ServiceContext.SessionId"/>
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
/// Tenant is set in exactly two places, both of them after the value has been proven:
/// <c>AddTikiJwtAuth</c>'s token-validated handler (from the live session) and
/// <see cref="Auth.ServiceRequestAuthenticationMiddleware"/> (once the HMAC signature
/// covering the header has verified). The header is still read here, but only to log it as
/// <c>UnverifiedTenantIdHeader</c> — a diagnostic, never an input to an access decision.
/// </para>
///
/// <para>
/// The tenant and user on the completion line come from a <see cref="RequestLogContext"/>
/// created here before <c>next</c>, which those two auth steps write into. Reading
/// <see cref="ServiceContext"/> after <c>await next()</c> returned null every time: an
/// AsyncLocal set further down the pipeline never flows back up to this frame.
/// </para>
///
/// <para>
/// The line's text is parsed by Alloy for services still logging plain text, so the order of
/// the fields up to <c>trace</c> is load-bearing; new fields go on the end. <c>TraceId</c> is
/// Tempo's 32-hex trace id (<see cref="TikiTraceIds"/>), not the raw traceparent.
/// </para>
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public const string TenantHeaderName = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        ServiceContext.SessionId = Guid.NewGuid();
        var identity = RequestLogContext.GetOrCreate(context);

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
                "{RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMs}ms — client {ClientIp}, tenant {TenantId}, session {SessionId}, caller {CallingService}, trace {TraceId}, unverifiedTenantHeader {UnverifiedTenantIdHeader}, user {UserId}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                clientIp ?? "unknown",
                identity.TenantId ?? ServiceContext.TenantId,
                ServiceContext.SessionId,
                ServiceContext.CallingService ?? "unknown",
                TikiTraceIds.CurrentTraceId() ?? "none",
                unverifiedTenantHeader ?? "none",
                identity.UserId ?? ServiceContext.UserId);
        }
    }
}
