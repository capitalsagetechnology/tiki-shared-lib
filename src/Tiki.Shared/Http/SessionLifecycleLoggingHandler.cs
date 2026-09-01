using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Auth;

namespace Tiki.Shared.Http;

/// <summary>
/// Logs three stages for every outbound call made through a client this handler is
/// attached to: <b>started</b> (method and path only — never the query string or body,
/// since either can carry secrets), <b>completed</b> (status code and elapsed
/// milliseconds), and <b>failed</b> (the exception and elapsed milliseconds). Every line
/// is tagged with <see cref="ServiceContext.SessionId"/> and <see cref="ServiceContext.TraceId"/>,
/// so filtering log output by one session id shows the complete, ordered, timed lifecycle
/// of everything one inbound request triggered downstream — not just the first call.
/// </summary>
public sealed class SessionLifecycleLoggingHandler(ILogger<SessionLifecycleLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var method = request.Method;
        var path = request.RequestUri?.AbsolutePath ?? "(unknown)";
        var sessionId = ServiceContext.SessionId;
        var traceId = ServiceContext.TraceId;

        logger.LogInformation(
            "Outbound call started: {Method} {Path} — session {SessionId}, trace {TraceId}",
            method, path, sessionId, traceId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            logger.LogInformation(
                "Outbound call completed: {Method} {Path} responded {StatusCode} in {ElapsedMs}ms — session {SessionId}, trace {TraceId}",
                method, path, (int)response.StatusCode, stopwatch.Elapsed.TotalMilliseconds, sessionId, traceId);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            logger.LogWarning(
                ex,
                "Outbound call failed: {Method} {Path} after {ElapsedMs}ms — session {SessionId}, trace {TraceId}",
                method, path, stopwatch.Elapsed.TotalMilliseconds, sessionId, traceId);

            throw;
        }
    }
}
