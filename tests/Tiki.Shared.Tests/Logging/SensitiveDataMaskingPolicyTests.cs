using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Tiki.Shared.Core.Attributes;
using Tiki.Shared.Logging;
using Xunit;

namespace Tiki.Shared.Tests.Logging;

/// <summary>Proves the masking policy actually changes emitted log output — not just that <c>[Sensitive]</c> compiles.</summary>
public class SensitiveDataMaskingPolicyTests
{
    private sealed class LoginRequest
    {
        public required string Username { get; init; }

        [Sensitive]
        public required string Password { get; init; }

        [Sensitive(SensitiveMaskStrategy.LastFourVisible)]
        public required string CardNumber { get; init; }

        [Sensitive(SensitiveMaskStrategy.Hashed)]
        public required string Ssn { get; init; }
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public LogEvent? LastEvent { get; private set; }
        public void Emit(LogEvent logEvent) => LastEvent = logEvent;
    }

    private static (Logger Logger, CapturingSink Sink) CreateLogger()
    {
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration()
            .Destructure.With<SensitiveDataMaskingPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, sink);
    }

    private static string RenderLastEvent(CapturingSink sink)
    {
        var formatter = new MessageTemplateTextFormatter("{Message:lj}");
        using var writer = new StringWriter();
        formatter.Format(sink.LastEvent!, writer);
        return writer.ToString();
    }

    private static LoginRequest SampleRequest() => new()
    {
        Username = "amaka",
        Password = "correct horse battery staple",
        CardNumber = "4111111111111111",
        Ssn = "123-45-6789",
    };

    [Fact]
    public void FullRedact_strategy_replaces_the_value_entirely()
    {
        var (logger, sink) = CreateLogger();
        using var _ = logger;

        logger.Information("Login attempt {@Request}", SampleRequest());
        var rendered = RenderLastEvent(sink);

        Assert.DoesNotContain("correct horse battery staple", rendered);
        Assert.Contains("REDACTED", rendered);
    }

    [Fact]
    public void LastFourVisible_strategy_keeps_only_the_last_four_characters()
    {
        var (logger, sink) = CreateLogger();
        using var _ = logger;

        logger.Information("Login attempt {@Request}", SampleRequest());
        var rendered = RenderLastEvent(sink);

        Assert.DoesNotContain("411111111111", rendered);
        Assert.Contains("1111", rendered);
    }

    [Fact]
    public void Hashed_strategy_never_emits_the_raw_value()
    {
        var (logger, sink) = CreateLogger();
        using var _ = logger;

        logger.Information("Login attempt {@Request}", SampleRequest());
        var rendered = RenderLastEvent(sink);

        Assert.DoesNotContain("123-45-6789", rendered);
    }

    [Fact]
    public void Non_sensitive_properties_are_still_logged_in_full()
    {
        var (logger, sink) = CreateLogger();
        using var _ = logger;

        logger.Information("Login attempt {@Request}", SampleRequest());
        var rendered = RenderLastEvent(sink);

        Assert.Contains("amaka", rendered);
    }

    [Fact]
    public void A_type_with_no_sensitive_properties_is_left_to_default_destructuring()
    {
        var (logger, sink) = CreateLogger();
        using var _ = logger;

        logger.Information("Plain object {@Value}", new { Foo = "bar" });
        var rendered = RenderLastEvent(sink);

        Assert.Contains("bar", rendered);
    }
}
