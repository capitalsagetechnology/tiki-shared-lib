using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Auth.Sessions;

namespace Tiki.Shared.Auth.Authorization;

/// <summary>
/// Evaluates <see cref="PermissionRequirement"/> against the session loaded for the current
/// request, in the tenant that request is operating in.
/// </summary>
/// <remarks>
/// Pure in-memory set membership — the Redis read already happened once, at authentication
/// time. The tenant comes from <see cref="ServiceContext.TenantId"/>, which is only ever set
/// from a validated source: the gateway's access-checked stamp, or a signed inter-service
/// request. That is the whole reason a permission check can be a dictionary lookup rather
/// than a query.
/// </remarks>
public sealed class PermissionAuthorizationHandler(
    ISessionAccessor sessionAccessor,
    ILogger<PermissionAuthorizationHandler> logger) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var session = sessionAccessor.Session;
        if (session is null)
        {
            // Deliberately not calling Fail(): leaving the requirement unmet lets the pipeline
            // answer 401 (authenticate) rather than 403 (you may not), which is the right
            // answer for a request carrying no session at all.
            return Task.CompletedTask;
        }

        var activeTenantId = sessionAccessor.ActiveTenantId;

        // A tenant-scoped request must be in a tenant this session can reach. Checked before
        // permissions, because "not your tenant" and "not enough permission in your tenant"
        // are different failures — and conflating them would let a caller probe which tenant
        // ids exist by comparing responses.
        if (activeTenantId is { } tenantId && !session.CanAccessTenant(tenantId))
        {
            logger.LogWarning(
                "User {UserId} attempted to act in tenant {TenantId}, which their session does not grant.",
                session.UserId, tenantId);

            context.Fail(new AuthorizationFailureReason(this, "No access to this tenant."));
            return Task.CompletedTask;
        }

        var granted = requirement.RequireAny
            ? session.HasAnyPermission(requirement.Permissions, activeTenantId)
            : session.HasAllPermissions(requirement.Permissions, activeTenantId);

        if (granted)
        {
            context.Succeed(requirement);
        }
        else
        {
            // Logged at the point of denial with the user, the tenant and what was missing —
            // otherwise a 403 in production is unattributable and every one becomes a ticket.
            logger.LogWarning(
                "Permission denied for user {UserId} in tenant {TenantId}: required {Mode} of [{Required}], holds [{Held}].",
                session.UserId,
                activeTenantId,
                requirement.RequireAny ? "any" : "all",
                string.Join(", ", requirement.Permissions),
                string.Join(", ", session.EffectivePermissions(activeTenantId)));

            context.Fail(new AuthorizationFailureReason(this, "Missing required permission."));
        }

        return Task.CompletedTask;
    }
}
