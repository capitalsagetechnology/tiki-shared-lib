namespace Tiki.Shared.Auth;

/// <summary>
/// Ambient, <see cref="AsyncLocal{T}"/>-backed accessor for the current request's trace id
/// and calling-service id. Readable anywhere in a request's call graph — including inside a
/// Kafka consumer handling a message — without either value being passed as a parameter.
/// Resolved through a claims → header → thread-local fallback chain by
/// <see cref="Core.Middleware.CorrelationIdMiddleware"/> and
/// <see cref="ServiceTokenValidationMiddleware"/> at the edge of the request.
/// </summary>
public static class ServiceContext
{
    private static readonly AsyncLocal<string?> TraceIdLocal = new();
    private static readonly AsyncLocal<string?> CallingServiceLocal = new();
    private static readonly AsyncLocal<Guid?> TenantIdLocal = new();
    private static readonly AsyncLocal<Guid?> SessionIdLocal = new();

    /// <summary>The trace id for the current logical call chain. Never null once set at the request edge.</summary>
    public static string TraceId
    {
        get => TraceIdLocal.Value ?? "unset-trace-id";
        set => TraceIdLocal.Value = value;
    }

    /// <summary>The id of the service that initiated the current call, e.g. <c>"wallet-service"</c>.</summary>
    public static string? CallingService
    {
        get => CallingServiceLocal.Value;
        set => CallingServiceLocal.Value = value;
    }

    /// <summary>
    /// The tenant the current request belongs to, if any — set by
    /// <see cref="Logging.RequestLoggingMiddleware"/> from the inbound <c>X-Tenant-Id</c>
    /// header. Null for a request that never carried one (an internal/unauthenticated
    /// endpoint, for example). This is the same ambient source
    /// <c>Persistence.ModelBuilderExtensions.ApplyTikiConventions</c> and
    /// <c>Persistence.TenantAuditSaveChangesInterceptor</c> read from.
    /// </summary>
    public static Guid? TenantId
    {
        get => TenantIdLocal.Value;
        set => TenantIdLocal.Value = value;
    }

    /// <summary>
    /// A GUID minted once per inbound request by <see cref="Logging.RequestLoggingMiddleware"/>,
    /// ambient for the lifetime of that request. Every outbound call made while handling
    /// one inbound request shares the same <see cref="SessionId"/>, so
    /// <see cref="Http.SessionLifecycleLoggingHandler"/>'s started/completed/failed log
    /// lines for all of them can be correlated by filtering on this one value — showing
    /// the complete, ordered, timed lifecycle of everything one inbound request triggered
    /// downstream, not just the first call.
    /// </summary>
    public static Guid? SessionId
    {
        get => SessionIdLocal.Value;
        set => SessionIdLocal.Value = value;
    }

    /// <summary>
    /// Sets both ambient values for the duration of a scope and restores the previous
    /// values on dispose — used by the Kafka consumer to seed context per message.
    /// </summary>
    public static IDisposable BeginScope(string traceId, string? callingService)
    {
        var previousTraceId = TraceIdLocal.Value;
        var previousCallingService = CallingServiceLocal.Value;

        TraceIdLocal.Value = traceId;
        CallingServiceLocal.Value = callingService;

        return new Scope(() =>
        {
            TraceIdLocal.Value = previousTraceId;
            CallingServiceLocal.Value = previousCallingService;
        });
    }

    private sealed class Scope(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
