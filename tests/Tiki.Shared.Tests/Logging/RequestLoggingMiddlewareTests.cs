using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Auth;
using Tiki.Shared.Logging;
using Xunit;

namespace Tiki.Shared.Tests.Logging;

public class RequestLoggingMiddlewareTests
{
    private sealed class CapturingLogger : ILogger<RequestLoggingMiddleware>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    [Fact]
    public async Task Logs_exactly_one_line_per_request()
    {
        var logger = new CapturingLogger();
        var middleware = new RequestLoggingMiddleware(_ => Task.CompletedTask, logger);

        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.Single(logger.Messages);
    }

    [Fact]
    public async Task Logged_line_includes_method_path_and_status_code()
    {
        var logger = new CapturingLogger();
        var middleware = new RequestLoggingMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = 201;
                return Task.CompletedTask;
            },
            logger);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/wallets";

        await middleware.InvokeAsync(context);

        var line = Assert.Single(logger.Messages);
        Assert.Contains("POST", line);
        Assert.Contains("/wallets", line);
        Assert.Contains("201", line);
    }

    [Fact]
    public async Task Never_sets_TenantId_from_the_X_Tenant_Id_header()
    {
        // Regression test for a cross-tenant data-access hole.
        //
        // This middleware runs before authentication, so anything it reads off the request
        // came straight from the caller. It used to copy X-Tenant-Id into
        // ServiceContext.TenantId — which is what drives the EF Core global tenant query
        // filter in every service. Any client could therefore send the header and read
        // another tenant's rows.
        //
        // Tenant is now established only after it has been proven: from the live session in
        // AddTikiJwtAuth's token-validated handler, or from a request whose HMAC signature
        // covers the header. A logging concern must not establish security context.
        var spoofed = Guid.NewGuid();
        Guid? observedTenantId = null;
        var logger = new CapturingLogger();
        var middleware = new RequestLoggingMiddleware(
            _ =>
            {
                observedTenantId = ServiceContext.TenantId;
                return Task.CompletedTask;
            },
            logger);

        var context = new DefaultHttpContext();
        context.Request.Headers[RequestLoggingMiddleware.TenantHeaderName] = spoofed.ToString();

        await middleware.InvokeAsync(context);

        Assert.Null(observedTenantId);
    }

    [Fact]
    public async Task Logs_the_unverified_tenant_header_without_trusting_it()
    {
        // Still worth recording — a spoofing attempt should be visible in the logs — but
        // labelled as unverified, and never fed into an access decision.
        var spoofed = Guid.NewGuid();
        var logger = new CapturingLogger();
        var middleware = new RequestLoggingMiddleware(_ => Task.CompletedTask, logger);

        var context = new DefaultHttpContext();
        context.Request.Headers[RequestLoggingMiddleware.TenantHeaderName] = spoofed.ToString();

        await middleware.InvokeAsync(context);

        var line = Assert.Single(logger.Messages);
        Assert.Contains(spoofed.ToString(), line, StringComparison.Ordinal);
        Assert.Contains("unverifiedTenantHeader", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mints_a_SessionId_for_every_request()
    {
        Guid? observedSessionId = null;
        var logger = new CapturingLogger();
        var middleware = new RequestLoggingMiddleware(
            _ =>
            {
                observedSessionId = ServiceContext.SessionId;
                return Task.CompletedTask;
            },
            logger);

        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.NotNull(observedSessionId);
    }

    [Fact]
    public async Task Two_separate_requests_get_two_different_SessionIds()
    {
        var logger = new CapturingLogger();
        var observedSessionIds = new List<Guid?>();
        var middleware = new RequestLoggingMiddleware(
            _ =>
            {
                observedSessionIds.Add(ServiceContext.SessionId);
                return Task.CompletedTask;
            },
            logger);

        await middleware.InvokeAsync(new DefaultHttpContext());
        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.Equal(2, observedSessionIds.Count);
        Assert.NotEqual(observedSessionIds[0], observedSessionIds[1]);
    }

    [Fact]
    public async Task A_malformed_tenant_header_leaves_TenantId_unset()
    {
        Guid? observedTenantId = null;
        var logger = new CapturingLogger();
        var middleware = new RequestLoggingMiddleware(
            _ =>
            {
                observedTenantId = ServiceContext.TenantId;
                return Task.CompletedTask;
            },
            logger);

        var context = new DefaultHttpContext();
        context.Request.Headers[RequestLoggingMiddleware.TenantHeaderName] = "not-a-guid";

        await middleware.InvokeAsync(context);

        Assert.Null(observedTenantId);
    }

    [Fact]
    public async Task Still_logs_when_the_downstream_pipeline_throws()
    {
        var logger = new CapturingLogger();
        var middleware = new RequestLoggingMiddleware(_ => throw new InvalidOperationException("boom"), logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(new DefaultHttpContext()));

        Assert.Single(logger.Messages);
    }
}
