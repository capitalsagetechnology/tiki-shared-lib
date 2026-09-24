using Tiki.Shared.Auth.Sessions;
using Tiki.Shared.Results;

namespace Tiki.Shared.Auth.Authorization;

/// <summary>
/// Whether a caller may act for a specific business — the check <c>[RequiresBusinessPermission]</c>
/// cannot make on its own, since the attribute has no route to read a business id from and can
/// only answer "holds this permission somewhere."
/// </summary>
/// <remarks>
/// Cross-service, not Identity-specific: any service that reads <see cref="TikiSession"/> out of
/// Redis can ask this question directly, since <see cref="TikiSession.BusinessAccess"/> already
/// carries every business the caller belongs to. Identity's own business-team-member logic
/// (invites, escalation checks against a caller's own grants, and so on) still lives in Identity —
/// this only holds the generic "does the session's grant for business X cover module/action" check.
/// </remarks>
public static class BusinessGrantAuthority
{
    // Same message regardless of cause, deliberately: distinguishing "that business exists but
    // is not yours" from "you cannot do that there" would let a caller enumerate business ids by
    // comparing responses.
    private static readonly Error AccessDenied = Error.Forbidden(
        "Business.AccessDenied", "You do not have permission to do that for this business.");

    public static Result CanManage(TikiSession caller, Guid businessId, BusinessModule module, PermissionAction action) =>
        caller.HasBusinessPermission(businessId, module, action) ? Result.Success() : Result.Failure(AccessDenied);

    /// <summary>The caller must hold every one of the given (module, action) pairs for this business.</summary>
    public static Result CanManage(TikiSession caller, Guid businessId, IEnumerable<BusinessPermission> permissions)
    {
        if (!caller.BusinessAccess.TryGetValue(businessId, out var grant))
            return Result.Failure(AccessDenied);

        var required = BusinessPermission.Expand(permissions);
        return required.All(p => grant.Permissions.Contains(p, StringComparer.Ordinal))
            ? Result.Success()
            : Result.Failure(AccessDenied);
    }
}
