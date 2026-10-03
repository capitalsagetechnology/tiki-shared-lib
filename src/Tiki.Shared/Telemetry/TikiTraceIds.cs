using System.Diagnostics;
using Tiki.Shared.Auth;

namespace Tiki.Shared.Telemetry;

/// <summary>
/// The trace and span id every log line carries, in exactly the form Tempo stores them: a
/// 32-hex trace id and a 16-hex span id.
/// </summary>
/// <remarks>
/// <see cref="ServiceContext.TraceId"/> is deliberately left as it is — it is what goes out in
/// <c>X-Correlation-Id</c> and the Kafka <c>traceparent</c> header, so on HTTP paths it holds the
/// whole W3C traceparent (<c>00-&lt;trace&gt;-&lt;span&gt;-01</c>) and on Kafka consumers a bare
/// trace id. Logging that raw value meant a log line and its trace only joined after Alloy had
/// split the traceparent apart. This type normalises it for logging only; propagation is
/// untouched.
/// </remarks>
public static class TikiTraceIds
{
    /// <summary>
    /// <see cref="Activity.Current"/>'s trace id when there is one, else the trace id inside
    /// <see cref="ServiceContext.TraceId"/>. Null when neither exists.
    /// </summary>
    public static string? CurrentTraceId()
    {
        var activity = Activity.Current;
        if (activity is not null && activity.TraceId != default)
            return activity.TraceId.ToHexString();

        return Normalize(ServiceContext.TraceIdOrNull);
    }

    /// <summary><see cref="Activity.Current"/>'s span id, else the span inside a traceparent-shaped <see cref="ServiceContext.TraceId"/>.</summary>
    public static string? CurrentSpanId()
    {
        var activity = Activity.Current;
        if (activity is not null && activity.SpanId != default)
            return activity.SpanId.ToHexString();

        return TryParseTraceParent(ServiceContext.TraceIdOrNull, out _, out var spanId) ? spanId : null;
    }

    /// <summary>
    /// The 32-hex trace id inside <paramref name="value"/>: a W3C traceparent is split, a bare
    /// trace id is lower-cased, and anything else (a caller-supplied correlation id) is returned
    /// unchanged so it is still searchable.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (TryParseTraceParent(value, out var traceId, out _))
            return traceId;

        return value.Length == 32 && IsHex(value) ? value.ToLowerInvariant() : value;
    }

    private static bool TryParseTraceParent(string? value, out string traceId, out string spanId)
    {
        traceId = spanId = string.Empty;

        // version(2)-traceid(32)-spanid(16)-flags(2)
        if (value is not { Length: 55 } || value[2] != '-' || value[35] != '-' || value[52] != '-')
            return false;

        var trace = value.AsSpan(3, 32);
        var span = value.AsSpan(36, 16);
        if (!IsHex(trace) || !IsHex(span))
            return false;

        traceId = trace.ToString().ToLowerInvariant();
        spanId = span.ToString().ToLowerInvariant();
        return true;
    }

    private static bool IsHex(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            if (!char.IsAsciiHexDigit(c))
                return false;
        }

        return true;
    }
}
