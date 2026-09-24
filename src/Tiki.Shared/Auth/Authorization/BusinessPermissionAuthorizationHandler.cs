using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Auth.Sessions;

namespace Tiki.Shared.Auth.Authorization;

/// <summary>
/// Evaluates <see cref="BusinessPermissionRequirement"/> against the session loaded for the
/// current request.
/// </summary>
/// <remarks>
/// Checked against every business the session belongs to
/// (<see cref="TikiSession.HasBusinessPermission(string)"/>), not a single active one — a user
/// can be a team member of more than one business. This is deliberately the coarse "holds it
/// somewhere" check: the attribute has no route to read a specific business id from, so it
/// cannot answer "holds it for <em>this</em> business." That question belongs to the service
/// layer, via <see cref="BusinessGrantAuthority.CanManage(TikiSession, Guid, BusinessModule, PermissionAction)"/>,
/// the same way the platform's own <see cref="PermissionAuthorizationHandler"/> only answers "is
/// this permission held" and leaves tenant-specific decisions to the caller.
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

        if (session.BusinessAccess.Count == 0)
        {
            logger.LogWarning(
                "User {UserId} attempted a business-permission check with no business on their session.",
                session.UserId);

            context.Fail(new AuthorizationFailureReason(this, "This session is not acting for a business."));
            return Task.CompletedTask;
        }

        var granted = requirement.RequireAny
            ? requirement.Permissions.Any(session.HasBusinessPermission)
            : requirement.Permissions.All(session.HasBusinessPermission);

        if (granted)
        {
            context.Succeed(requirement);
        }
        else
        {
            logger.LogWarning(
                "Business permission denied for user {UserId}: required {Mode} of [{Required}].",
                session.UserId,
                requirement.RequireAny ? "any" : "all",
                string.Join(", ", requirement.Permissions));

            context.Fail(new AuthorizationFailureReason(this, "Missing required business permission."));
        }

        return Task.CompletedTask;
    }
}
