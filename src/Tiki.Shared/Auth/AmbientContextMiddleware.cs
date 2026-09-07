using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Tiki.Shared.Auth.Sessions;

namespace Tiki.Shared.Auth;

/// <summary>
/// Copies the authenticated identity from <c>HttpContext.Items</c> onto
/// <see cref="ServiceContext"/> so the rest of the request can read it ambiently.
/// </summary>
/// <remarks>
/// This exists because of a genuinely surprising <see cref="AsyncLocal{T}"/> behaviour, and it
/// is worth stating plainly: execution context is copy-on-write, so an <c>AsyncLocal</c> set
/// <em>inside</em> an awaited call is not visible to the caller once that call returns. The JWT
/// bearer handler resolves the session and the active tenant deep inside its own await chain,
/// so writing <see cref="ServiceContext.TenantId"/> there and reading it from the authorization
/// handler silently yields null.
///
/// <para>
/// Setting the same values from a middleware, before <c>await next()</c>, does work — that
/// write happens in the pipeline's own context and flows to everything downstream, including
/// the EF Core tenant query filter, which is the thing that must never be wrong.
/// </para>
///
/// <para>Place it immediately after <c>UseAuthentication()</c>.</para>
/// </remarks>
public sealed class AmbientContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (context.Items.TryGetValue(HttpContextSessionAccessor.ItemsKey, out var sessionValue) &&
            sessionValue is TikiSession session)
        {
            ServiceContext.UserId = session.UserId;
            ServiceContext.UserType = session.UserType;
        }

        if (context.Items.TryGetValue(HttpContextSessionAccessor.ActiveTenantItemsKey, out var tenantValue) &&
            tenantValue is Guid tenantId)
        {
            ServiceContext.TenantId = tenantId;
        }

        return next(context);
    }
}

public static class AmbientContextExtensions
{
    /// <summary>
    /// Hydrates <see cref="ServiceContext"/> from the authenticated request. Register directly
    /// after <c>UseAuthentication()</c> and before <c>UseAuthorization()</c>.
    /// </summary>
    public static IApplicationBuilder UseTikiAmbientContext(this IApplicationBuilder app) =>
        app.UseMiddleware<AmbientContextMiddleware>();
}
