using Microsoft.AspNetCore.Http;

namespace Tiki.Shared.Logging;

/// <summary>
/// The verified identity of the current request, recorded for
/// <see cref="RequestLoggingMiddleware"/>'s completion line.
/// </summary>
/// <remarks>
/// <para>
/// This exists because of the same <see cref="AsyncLocal{T}"/> behaviour described on
/// <see cref="Auth.AmbientContextMiddleware"/>: execution context is copy-on-write, so the
/// <see cref="Auth.ServiceContext.TenantId"/> that authentication sets further down the
/// pipeline is not visible to <see cref="RequestLoggingMiddleware"/> once <c>await next()</c>
/// returns — every request line used to say <c>tenant null</c>, authenticated or not.
/// </para>
/// <para>
/// A reference type, by contrast, is shared: the middleware creates one instance and stores it
/// on <see cref="HttpContext.Items"/> before calling <c>next</c>, the authentication steps write
/// the values they have just verified into that same instance, and the middleware reads them
/// back after <c>next</c> returns. Only ids are recorded, never names or contact details.
/// </para>
/// <para>
/// This is logging state only. It must never be read for an access decision — that is what
/// <see cref="Auth.ServiceContext"/> is for.
/// </para>
/// </remarks>
public sealed class RequestLogContext
{
    /// <summary>The <see cref="HttpContext.Items"/> key the instance is stored under.</summary>
    public static readonly object ItemsKey = new();

    /// <summary>The tenant authentication verified for this request.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>The end user authentication verified for this request.</summary>
    public Guid? UserId { get; set; }

    /// <summary>The holder for <paramref name="context"/>, or null when <see cref="RequestLoggingMiddleware"/> is not in the pipeline.</summary>
    public static RequestLogContext? Get(HttpContext context) =>
        context.Items.TryGetValue(ItemsKey, out var value) ? value as RequestLogContext : null;

    /// <summary>Returns the holder for <paramref name="context"/>, creating it if absent.</summary>
    public static RequestLogContext GetOrCreate(HttpContext context)
    {
        if (Get(context) is { } existing)
            return existing;

        var created = new RequestLogContext();
        context.Items[ItemsKey] = created;
        return created;
    }

    /// <summary>
    /// Records a verified identity. A null argument leaves the existing value alone, so a later
    /// step that knows only the user never erases a tenant an earlier step recorded.
    /// </summary>
    public static void Record(HttpContext context, Guid? tenantId, Guid? userId)
    {
        if (Get(context) is not { } holder)
            return;

        if (tenantId is not null)
            holder.TenantId = tenantId;
        if (userId is not null)
            holder.UserId = userId;
    }
}
