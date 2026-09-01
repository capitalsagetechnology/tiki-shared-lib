using System.Net;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Auth;
using Tiki.Shared.Http;
using Xunit;

namespace Tiki.Shared.Tests.Http;

public class SessionLifecycleLoggingHandlerTests
{
    private sealed class CapturingLogger : ILogger<SessionLifecycleLoggingHandler>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class StubInnerHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class ThrowingInnerHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }

    private static (SessionLifecycleLoggingHandler Handler, CapturingLogger Logger) CreateHandler(HttpMessageHandler inner)
    {
        var logger = new CapturingLogger();
        var handler = new SessionLifecycleLoggingHandler(logger) { InnerHandler = inner };
        return (handler, logger);
    }

    [Fact]
    public async Task Logs_started_then_completed_for_a_successful_call()
    {
        var (handler, logger) = CreateHandler(new StubInnerHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        using var invoker = new HttpMessageInvoker(handler);

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://compliance.internal/verify"), CancellationToken.None);

        Assert.Equal(2, logger.Entries.Count);
        Assert.Contains("started", logger.Entries[0].Message);
        Assert.Contains("completed", logger.Entries[1].Message);
        Assert.Contains("200", logger.Entries[1].Message);
    }

    [Fact]
    public async Task Never_logs_the_query_string()
    {
        var (handler, logger) = CreateHandler(new StubInnerHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        using var invoker = new HttpMessageInvoker(handler);

        await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://compliance.internal/verify?ssn=123-45-6789"),
            CancellationToken.None);

        Assert.All(logger.Entries, entry => Assert.DoesNotContain("ssn=123-45-6789", entry.Message));
        Assert.All(logger.Entries, entry => Assert.Contains("/verify", entry.Message));
    }

    [Fact]
    public async Task Logs_failed_with_the_exception_when_the_inner_handler_throws()
    {
        var (handler, logger) = CreateHandler(new ThrowingInnerHandler(new HttpRequestException("connection reset")));
        using var invoker = new HttpMessageInvoker(handler);

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() =>
            invoker.SendAsync(new HttpRequestMessage(HttpMethod.Post, "https://compliance.internal/verify"), CancellationToken.None));

        Assert.Equal("connection reset", thrown.Message);
        Assert.Equal(2, logger.Entries.Count);
        Assert.Contains("started", logger.Entries[0].Message);
        var failedEntry = logger.Entries[1];
        Assert.Equal(LogLevel.Warning, failedEntry.Level);
        Assert.Contains("failed", failedEntry.Message);
    }

    [Fact]
    public async Task Every_line_is_tagged_with_the_ambient_session_and_trace_id()
    {
        var sessionId = Guid.NewGuid();
        using var _ = ServiceContext.BeginScope("trace-42", null);
        ServiceContext.SessionId = sessionId;

        var (handler, logger) = CreateHandler(new StubInnerHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        using var invoker = new HttpMessageInvoker(handler);

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://compliance.internal/verify"), CancellationToken.None);

        Assert.All(logger.Entries, entry => Assert.Contains(sessionId.ToString(), entry.Message));
        Assert.All(logger.Entries, entry => Assert.Contains("trace-42", entry.Message));
    }
}
