using Microsoft.AspNetCore.Authorization;

namespace Tiki.Shared.Auth.Authorization;

/// <summary>
/// Requires the caller's session to be acting for a business
/// (<see cref="Sessions.TikiSession.BusinessId"/> is set) and to hold a permission on one of
/// that business's own modules.
///
/// <code>
/// [RequiresBusinessPermission(BusinessModule.Store, PermissionAction.Write)]
/// public Task&lt;ActionResult&gt; CreateProduct(...) { }
/// </code>
///
/// <para>
/// This checks only "does the session generally hold this business permission" — not "is it
/// the specific business named in the route". That second check is where privilege escalation
/// lives (a team member of business A pointing a request at business B's id) and belongs at the
/// service layer, next to the rest of that decision, the same way <c>GrantAuthority</c> sits
/// beside <see cref="RequiresPermissionAttribute"/> rather than inside it.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresBusinessPermissionAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    private readonly string[] _permissions;

    /// <summary>Requires one module/action pair.</summary>
    public RequiresBusinessPermissionAttribute(BusinessModule module, PermissionAction action)
        : this([new BusinessPermission(module, action)])
    {
    }

    /// <summary>
    /// Requires an action across several modules — ANDed by default, see <see cref="RequireAny"/>.
    /// </summary>
    public RequiresBusinessPermissionAttribute(PermissionAction action, params BusinessModule[] modules)
        : this([.. modules.Select(m => new BusinessPermission(m, action))])
    {
    }

    private RequiresBusinessPermissionAttribute(BusinessPermission[] permissions)
    {
        if (permissions.Length == 0)
            throw new ArgumentException("Specify at least one permission.", nameof(permissions));

        _permissions = [.. permissions.Select(p => p.Value)];
    }

    /// <summary>Accept the caller if they hold <em>any</em> of the listed permissions rather than all.</summary>
    public bool RequireAny { get; init; }

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new BusinessPermissionRequirement(_permissions, RequireAny);
    }
}

/// <summary>The requirement <see cref="BusinessPermissionAuthorizationHandler"/> evaluates.</summary>
public sealed class BusinessPermissionRequirement(IReadOnlyList<string> permissions, bool requireAny) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; } = permissions;

    public bool RequireAny { get; } = requireAny;
}
