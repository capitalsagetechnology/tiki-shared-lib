using Microsoft.AspNetCore.Http;
using Tiki.Shared.Gateway;

namespace Tiki.Shared.Auth.Sessions;

/// <summary>The outcome of deciding which tenant a request acts in.</summary>
/// <param name="TenantId">The resolved tenant, or null when the request is not tenant-scoped.</param>
/// <param name="IsDenied">True when the caller asked for a tenant their session does not grant.</param>
public readonly record struct TenantSelectionResult(Guid? TenantId, bool IsDenied)
{
    public static TenantSelectionResult Denied => new(null, true);
    public static TenantSelectionResult None => new(null, false);
    public static TenantSelectionResult Selected(Guid tenantId) => new(tenantId, false);
}

/// <summary>
/// Decides which of a user's tenants a request operates in.
/// </summary>
/// <remarks>
/// A user may hold access to several tenants, so something has to choose one per request, and
/// only the client knows which. This is where that choice is validated — in one place, so the
/// gateway and any service validating a token directly cannot disagree about it.
/// </remarks>
public static class TenantSelection
{
    /// <summary>
    /// Resolves the active tenant from what the client asked for and what the session allows.
    /// </summary>
    /// <remarks>
    /// The fallback ladder is deliberately short, and stops short of guessing:
    /// <list type="number">
    /// <item>An explicit, granted selection wins.</item>
    /// <item>An explicit selection the session does not grant is <b>denied</b>, never silently
    /// replaced with a tenant the user does have — substituting one would show them another
    /// tenant's data under a heading naming the one they asked for.</item>
    /// <item>With no selection: the user's home tenant, or their single accessible tenant.</item>
    /// <item>With no selection and several tenants to choose from, the result is
    /// <see cref="TenantSelectionResult.None"/> — no tenant. Handlers that need one say so via
    /// <see cref="ISessionAccessor.RequireTenant"/> and the caller is told to choose. Picking
    /// the first is how someone edits the wrong tenant's settings.</item>
    /// </list>
    /// A global-scope session may select any tenant, including one created after it began.
    /// </remarks>
    public static TenantSelectionResult Resolve(TikiSession session, Guid? requestedTenantId)
    {
        if (requestedTenantId is { } requested)
        {
            return session.CanAccessTenant(requested)
                ? TenantSelectionResult.Selected(requested)
                : TenantSelectionResult.Denied;
        }

        if (session.HomeTenantId is { } home && session.CanAccessTenant(home))
            return TenantSelectionResult.Selected(home);

        return session.TenantAccess.Count == 1
            ? TenantSelectionResult.Selected(session.TenantAccess.Keys.First())
            : TenantSelectionResult.None;
    }

    /// <summary>
    /// Reads the client's tenant selection from <see cref="TikiHeaderNames.SelectTenant"/>.
    /// </summary>
    /// <remarks>
    /// Untrusted input — it is only ever a <em>request</em>, checked by <see cref="Resolve"/>
    /// against the session before it becomes the stamped <c>X-Tenant-Id</c>. A malformed value
    /// reads as "no selection" rather than as an error: a client sending rubbish should get the
    /// default tenant behaviour, not a failure it cannot interpret.
    /// </remarks>
    public static Guid? ReadRequested(HttpRequest request) =>
        Guid.TryParse(request.Headers[TikiHeaderNames.SelectTenant].FirstOrDefault(), out var tenantId)
            ? tenantId
            : null;
}
