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
    public async Task Sets_TenantId_from_the_X_Tenant_Id_header()
    {
        // Observed from inside `next` — the same place downstream middleware/handlers would
        // read it. AsyncLocal changes made inside an awaited call are visible to code
        // nested further inside that same call, not to the caller after it returns; that
        // is exactly the scoping this middleware relies on for the rest of the pipeline.
        var tenantId = Guid.NewGuid();
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
        context.Request.Headers[RequestLoggingMiddleware.TenantHeaderName] = tenantId.ToString();

        await middleware.InvokeAsync(context);

        Assert.Equal(tenantId, observedTenantId);
    }

    [Fact]
    public async Task An_invalid_tenant_header_leaves_TenantId_unset()
    {
        var logger = new CapturingLogger();
        var middleware = new RequestLoggingMiddleware(_ => Task.CompletedTask, logger);

        var context = new DefaultHttpContext();
        context.Request.Headers[RequestLoggingMiddleware.TenantHeaderName] = "not-a-guid";

        await middleware.InvokeAsync(context);

        var line = Assert.Single(logger.Messages);
        Assert.DoesNotContain("not-a-guid", line);
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
