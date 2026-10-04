using System.Diagnostics;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Tiki.Shared.Auth;
using Tiki.Shared.Logging;
using Xunit;

namespace Tiki.Shared.Tests.Logging;

public class TikiLogEnrichersTests
{
    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static (Logger Logger, CollectingSink Sink) CreateLogger()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .ConfigureTikiLogging("wallet-service")
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, sink);
    }

    private static object? Scalar(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) ? (value as ScalarValue)?.Value : null;

    [Fact]
    public async Task Adds_tenant_user_and_session_ids_from_ServiceContext()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var (logger, sink) = CreateLogger();

        await Task.Run(() =>
        {
            ServiceContext.TenantId = tenantId;
            ServiceContext.UserId = userId;
            ServiceContext.SessionId = sessionId;
            ServiceContext.UserType = "Customer";
            logger.Information("hello");
        });

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal(tenantId, Scalar(logEvent, "TenantId"));
        Assert.Equal(userId, Scalar(logEvent, "UserId"));
        Assert.Equal(sessionId, Scalar(logEvent, "SessionId"));
        Assert.Equal("wallet-service", Scalar(logEvent, "ServiceName"));
    }

    [Fact]
    public void Adds_nothing_identifying_when_there_is_no_identity()
    {
        var (logger, sink) = CreateLogger();

        logger.Information("hello");

        var logEvent = Assert.Single(sink.Events);
        Assert.False(logEvent.Properties.ContainsKey("TenantId"));
        Assert.False(logEvent.Properties.ContainsKey("UserId"));
        Assert.False(logEvent.Properties.ContainsKey("UserType"));
    }

    [Fact]
    public void A_property_the_log_call_supplies_wins_over_the_ambient_one()
    {
        var explicitTenant = Guid.NewGuid();
        var (logger, sink) = CreateLogger();

        logger.Information("For {TenantId}", explicitTenant);

        Assert.Equal(explicitTenant, Scalar(Assert.Single(sink.Events), "TenantId"));
    }

    [Fact]
    public void Http_path_logs_the_tempo_trace_and_span_id_not_the_traceparent()
    {
        using var activity = new Activity("inbound").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var _ = ServiceContext.BeginScope(activity.Id!, null);   // what CorrelationIdMiddleware stores
        var (logger, sink) = CreateLogger();

        logger.Information("hello");

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal(activity.TraceId.ToHexString(), Scalar(logEvent, "TraceId"));
        Assert.Equal(activity.SpanId.ToHexString(), Scalar(logEvent, "SpanId"));
        Assert.Matches("^[0-9a-f]{32}$", (string)Scalar(logEvent, "TraceId")!);
    }

    [Fact]
    public void Kafka_path_without_an_activity_logs_the_trace_inside_the_traceparent_header()
    {
        // TikiConsumerBackgroundService seeds ServiceContext from the message's traceparent when no
        // listener created a consume activity.
        const string traceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        Assert.Null(Activity.Current);
        using var _ = ServiceContext.BeginScope(traceParent, null);
        var (logger, sink) = CreateLogger();

        logger.Information("consumed");

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", Scalar(logEvent, "TraceId"));
        Assert.Equal("00f067aa0ba902b7", Scalar(logEvent, "SpanId"));
    }

    [Fact]
    public void Kafka_path_with_a_consume_activity_logs_its_trace_id()
    {
        using var activity = new Activity("consume topic").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var _ = ServiceContext.BeginScope(activity.TraceId.ToString(), null);
        var (logger, sink) = CreateLogger();

        logger.Information("consumed");

        Assert.Equal(activity.TraceId.ToHexString(), Scalar(Assert.Single(sink.Events), "TraceId"));
    }

    [Fact]
    public void No_trace_id_is_invented_when_there_is_none()
    {
        Assert.Null(Activity.Current);
        var (logger, sink) = CreateLogger();

        logger.Information("startup");

        Assert.False(Assert.Single(sink.Events).Properties.ContainsKey("TraceId"));
    }

    [Fact]
    public void LogContext_properties_reach_the_line()
    {
        var (logger, sink) = CreateLogger();

        using (LogContext.PushProperty("BusinessId", "b-1"))
            logger.Information("hello");

        Assert.Equal("b-1", Scalar(Assert.Single(sink.Events), "BusinessId"));
    }
}
