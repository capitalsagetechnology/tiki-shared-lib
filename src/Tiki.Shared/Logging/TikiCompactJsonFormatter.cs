using System.Globalization;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Json;
using Serilog.Parsing;

namespace Tiki.Shared.Logging;

/// <summary>
/// Serilog compact JSON (CLEF) — one object per line: <c>@t</c>, <c>@m</c>, <c>@l</c> (omitted
/// for Information), <c>@x</c>, <c>@tr</c>, <c>@sp</c>, then every property as its own field.
/// </summary>
/// <remarks>
/// <para>
/// The same shape as <c>Serilog.Formatting.Compact.RenderedCompactJsonFormatter</c>, with one
/// deliberate difference: <c>@m</c> renders string values <em>literally</em>, exactly as the text
/// console's <c>{Message:lj}</c> does. The stock formatter quotes them
/// (<c>"GET" "/api/x" responded 200</c>), so every regex written against our text logs — Alloy's
/// request-line parser, the dashboards' provider/outcome panels, ad-hoc LogQL — would silently
/// stop matching the moment a service switched to JSON. With this formatter <c>@m</c> is
/// byte-for-byte the text line's message, and the structured fields come on top.
/// </para>
/// <para>Any CLEF reader (Alloy's <c>stage.json</c>, Seq, <c>clef-tool</c>) reads it unchanged.</para>
/// </remarks>
public sealed class TikiCompactJsonFormatter : ITextFormatter
{
    private static readonly JsonValueFormatter ValueFormatter = new(typeTagName: "$type");

    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);

        output.Write("{\"@t\":\"");
        output.Write(logEvent.Timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        output.Write("\",\"@m\":");
        JsonValueFormatter.WriteQuotedJsonString(RenderLiteral(logEvent), output);

        if (logEvent.Level != LogEventLevel.Information)
        {
            output.Write(",\"@l\":\"");
            output.Write(logEvent.Level.ToString());
            output.Write('"');
        }

        if (logEvent.Exception is not null)
        {
            output.Write(",\"@x\":");
            JsonValueFormatter.WriteQuotedJsonString(logEvent.Exception.ToString(), output);
        }

        if (logEvent.TraceId is { } traceId && traceId != default)
        {
            output.Write(",\"@tr\":\"");
            output.Write(traceId.ToHexString());
            output.Write('"');
        }

        if (logEvent.SpanId is { } spanId && spanId != default)
        {
            output.Write(",\"@sp\":\"");
            output.Write(spanId.ToHexString());
            output.Write('"');
        }

        foreach (var (name, value) in logEvent.Properties)
        {
            output.Write(',');
            // CLEF reserves the '@' prefix; a property that starts with one is escaped by doubling it.
            JsonValueFormatter.WriteQuotedJsonString(name.StartsWith('@') ? "@" + name : name, output);
            output.Write(':');
            ValueFormatter.Format(value, output);
        }

        output.Write('}');
        output.WriteLine();
    }

    /// <summary>The message as <c>{Message:lj}</c> renders it: string values unquoted.</summary>
    internal static string RenderLiteral(LogEvent logEvent)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        foreach (var token in logEvent.MessageTemplate.Tokens)
        {
            if (token is PropertyToken property &&
                property.Format is null &&
                logEvent.Properties.TryGetValue(property.PropertyName, out var value) &&
                value is ScalarValue { Value: string text })
            {
                writer.Write(text);
            }
            else
            {
                token.Render(logEvent.Properties, writer, CultureInfo.InvariantCulture);
            }
        }

        return writer.ToString();
    }
}
