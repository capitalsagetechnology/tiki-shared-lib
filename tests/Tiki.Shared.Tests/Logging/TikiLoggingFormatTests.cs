using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Extensions.Logging;
using Tiki.Shared.Auth;
using Tiki.Shared.Auth.Sessions;
using Tiki.Shared.Logging;
using Xunit;

namespace Tiki.Shared.Tests.Logging;

[Collection(ConsoleCaptureCollection.Name)]
public class TikiLoggingFormatTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Theory]
    [InlineData("json", "Development", null, TikiLogFormat.Json)]
    [InlineData("text", "Production", "true", TikiLogFormat.Text)]
    [InlineData("JSON", "Development", null, TikiLogFormat.Json)]
    [InlineData(null, "Development", "true", TikiLogFormat.Json)]   // the local docker stack
    [InlineData(null, "Production", "true", TikiLogFormat.Json)]    // EKS
    [InlineData(null, "Development", "false", TikiLogFormat.Text)]  // dotnet run on a laptop
    [InlineData(null, "Staging", "false", TikiLogFormat.Json)]
    public void Resolves_the_console_format(string? format, string environment, string? inContainer, TikiLogFormat expected)
    {
        var configuration = Config(
            ("Tiki:Logging:Format", format),
            ("DOTNET_RUNNING_IN_CONTAINER", inContainer ?? "false"));

        Assert.Equal(expected, LoggingExtensions.ResolveFormat(configuration, environment));
    }

    [Fact]
    public void Json_format_writes_one_compact_json_object_per_line_with_every_property()
    {
        using var capture = new ConsoleCapture();
        using (var logger = new LoggerConfiguration()
                   .ConfigureTikiLogging("wallet-service", Config(("Tiki:Logging:Format", "json")))
                   .CreateLogger())
        {
            logger.Information("Credited {Amount} to wallet {WalletId}", 12.5m, "w-1");
        }

        using var json = JsonDocument.Parse(Assert.Single(capture.Lines));
        var root = json.RootElement;
        // Literal, like the text console's {Message:lj} — so regexes written against text logs still match.
        Assert.Equal("Credited 12.5 to wallet w-1", root.GetProperty("@m").GetString());
        Assert.False(root.TryGetProperty("@l", out _));   // CLEF omits Information
        Assert.Equal(12.5m, root.GetProperty("Amount").GetDecimal());
        Assert.Equal("w-1", root.GetProperty("WalletId").GetString());
        Assert.Equal("wallet-service", root.GetProperty("ServiceName").GetString());
    }

    [Fact]
    public void Json_format_carries_level_exception_and_escapes_reserved_property_names()
    {
        using var capture = new ConsoleCapture();
        using (var logger = new LoggerConfiguration()
                   .ConfigureTikiLogging("wallet-service", Config(("Tiki:Logging:Format", "json")))
                   .CreateLogger())
        {
            logger.ForContext("@odd", 1).Error(new InvalidOperationException("boom"), "Payout {PayoutId} failed", 7);
        }

        using var json = JsonDocument.Parse(Assert.Single(capture.Lines));
        var root = json.RootElement;
        Assert.Equal("Error", root.GetProperty("@l").GetString());
        Assert.StartsWith("System.InvalidOperationException: boom", root.GetProperty("@x").GetString());
        Assert.Equal("Payout 7 failed", root.GetProperty("@m").GetString());
        Assert.Equal(1, root.GetProperty("@@odd").GetInt32());
    }

    [Fact]
    public void Text_format_keeps_the_human_readable_template()
    {
        using var capture = new ConsoleCapture();
        using (var logger = new LoggerConfiguration()
                   .ConfigureTikiLogging("wallet-service", Config(("Tiki:Logging:Format", "text")))
                   .CreateLogger())
        {
            logger.Information("Credited {Amount}", 12.5m);
        }

        var line = Assert.Single(capture.Lines);
        Assert.Matches(@"^\[\d{2}:\d{2}:\d{2} INF\] Credited 12.5$", line);
    }

    [Fact]
    public void Otlp_sink_is_off_unless_enabled()
    {
        // Enabled=false with an endpoint configured must not try to export anywhere; building and
        // writing simply succeeds with the console as the only sink.
        using var capture = new ConsoleCapture();
        using (var logger = new LoggerConfiguration()
                   .ConfigureTikiLogging("wallet-service", Config(
                       ("Tiki:Logging:Format", "json"),
                       ("Tiki:Telemetry:OtlpEndpoint", "http://127.0.0.1:1")))
                   .CreateLogger())
        {
            logger.Information("hello");
        }

        Assert.Single(capture.Lines);
    }

    [Fact]
    public async Task An_authenticated_request_line_in_json_carries_tenant_user_and_tempo_trace_ids()
    {
        // End to end: the real middleware, the real auth hand-off (AmbientContextMiddleware reading
        // what the JWT handler put on HttpContext.Items), and the real console formatter.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var activity = new Activity("inbound").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var _ = ServiceContext.BeginScope(activity.Id!, callingService: null);

        using var capture = new ConsoleCapture();
        using (var serilog = new LoggerConfiguration()
                   .ConfigureTikiLogging("wallet-service", Config(("Tiki:Logging:Format", "json")))
                   .CreateLogger())
        using (var factory = new SerilogLoggerFactory(serilog))
        {
            var middleware = new RequestLoggingMiddleware(
                async ctx =>
                {
                    await Task.Yield();
                    ctx.Items[HttpContextSessionAccessor.ItemsKey] = Session(userId, tenantId);
                    ctx.Items[HttpContextSessionAccessor.ActiveTenantItemsKey] = tenantId;
                    await new AmbientContextMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);
                },
                Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<RequestLoggingMiddleware>(factory));

            var context = new DefaultHttpContext();
            context.Request.Method = "GET";
            context.Request.Path = "/api/wallets";
            await middleware.InvokeAsync(context);
        }

        using var json = JsonDocument.Parse(Assert.Single(capture.Lines));
        var root = json.RootElement;
        Assert.Equal(tenantId.ToString(), root.GetProperty("TenantId").GetString());
        Assert.Equal(userId.ToString(), root.GetProperty("UserId").GetString());
        Assert.Equal(activity.TraceId.ToHexString(), root.GetProperty("TraceId").GetString());
        Assert.Equal(activity.SpanId.ToHexString(), root.GetProperty("SpanId").GetString());
        Assert.Equal(activity.TraceId.ToHexString(), root.GetProperty("@tr").GetString());
        Assert.Equal("GET", root.GetProperty("RequestMethod").GetString());
        Assert.StartsWith("GET /api/wallets responded 200 in ", root.GetProperty("@m").GetString());
        Assert.Contains($"tenant {tenantId}, session ", root.GetProperty("@m").GetString());
        Assert.Equal("/api/wallets", root.GetProperty("RequestPath").GetString());
        Assert.Equal(200, root.GetProperty("StatusCode").GetInt32());
    }

    internal static TikiSession Session(Guid userId, Guid tenantId) => new()
    {
        SessionId = Guid.NewGuid(),
        UserId = userId,
        UserType = "BusinessOwner",
        HomeTenantId = tenantId,
        IssuedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
    };
}
