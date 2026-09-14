using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Auth.Sessions;

namespace Tiki.Shared.Auth.Authorization;

/// <summary>
/// Evaluates <see cref="BusinessPermissionRequirement"/> against the session loaded for the
/// current request.
/// </summary>
/// <remarks>
/// Business permissions are checked against the session's own home tenant
/// (<see cref="TikiSession.HasBusinessPermission"/>), not an active tenant selected per request
/// the way <see cref="PermissionAuthorizationHandler"/> does for Tiki-staff permissions. A
/// business team member has exactly one home tenant and one business — there is no "which
/// tenant is this request operating in" question to answer for them.
/// </remarks>
public sealed class BusinessPermissionAuthorizationHandler(
    ISessionAccessor sessionAccessor,
    ILogger<BusinessPermissionAuthorizationHandler> logger) : AuthorizationHandler<BusinessPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, BusinessPermissionRequirement requirement)
    {
        var session = sessionAccessor.Session;
        if (session is null)
        {
            // As with PermissionAuthorizationHandler: leave the requirement unmet so the
            // pipeline answers 401 (authenticate), not 403 (you may not).
            return Task.CompletedTask;
        }

        if (session.BusinessId is null)
        {
            logger.LogWarning(
                "User {UserId} attempted a business-permission check with no business on their session.",
                session.UserId);

            context.Fail(new AuthorizationFailureReason(this, "This session is not acting for a business."));
            return Task.CompletedTask;
        }

        var granted = requirement.RequireAny
            ? requirement.Permissions.Any(p => session.HasPermission(p, session.HomeTenantId))
            : requirement.Permissions.All(p => session.HasPermission(p, session.HomeTenantId));

        if (granted)
        {
            context.Succeed(requirement);
        }
        else
        {
            logger.LogWarning(
                "Business permission denied for user {UserId} (business {BusinessId}): required {Mode} of [{Required}].",
                session.UserId,
                session.BusinessId,
                requirement.RequireAny ? "any" : "all",
                string.Join(", ", requirement.Permissions));

            context.Fail(new AuthorizationFailureReason(this, "Missing required business permission."));
        }

        return Task.CompletedTask;
    }
}
