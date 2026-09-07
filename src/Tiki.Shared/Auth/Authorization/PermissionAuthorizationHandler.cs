using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Auth.Sessions;

namespace Tiki.Shared.Auth.Authorization;

/// <summary>
/// Evaluates <see cref="PermissionRequirement"/> against the session loaded for the current
/// request. Pure in-memory set membership — the Redis read already happened once, at
/// authentication time.
/// </summary>
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
            // Deliberately not calling Fail(): leaving the requirement unmet lets the
            // pipeline answer 401 (authenticate) rather than 403 (you may not), which is
            // the right answer for a request that has no session at all.
            return Task.CompletedTask;
        }

        var granted = requirement.RequireAny
            ? session.HasAnyPermission(requirement.Permissions)
            : session.HasAllPermissions(requirement.Permissions);

        if (granted)
        {
            context.Succeed(requirement);
        }
        else
        {
            // Logged at the point of denial with the user and what was missing — otherwise
            // a 403 in production is unattributable and every one becomes a support ticket.
            logger.LogWarning(
                "Permission denied for user {UserId} in tenant {TenantId}: required {Mode} of [{Required}], session holds [{Held}].",
                session.UserId,
                session.TenantId,
                requirement.RequireAny ? "any" : "all",
                string.Join(", ", requirement.Permissions),
                string.Join(", ", session.Permissions));

            context.Fail(new AuthorizationFailureReason(this, "Missing required permission."));
        }

        return Task.CompletedTask;
    }
}
