using Microsoft.AspNetCore.Authorization;

namespace Tiki.Shared.Auth.Authorization;

/// <summary>
/// Requires the caller's session to carry the named permission(s).
///
/// <code>
/// [RequiresPermission(TikiPermissions.TenantWrite)]
/// public Task&lt;IActionResult&gt; Update(...) { }
/// </code>
///
/// <para>
/// The check reads the permission set off the session already loaded for this request —
/// no database query, no call to Identity. Declaring the requirement on the endpoint rather
/// than writing an <c>if</c> in the handler is what makes it auditable: the set of
/// permissions a service enforces can be enumerated from its routes.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresPermissionAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    private readonly string[] _permissions;

    /// <param name="permissions">
    /// Permissions to require. Multiple values are ANDed by default — see
    /// <see cref="RequireAny"/> to switch to OR.
    /// </param>
    public RequiresPermissionAttribute(params string[] permissions)
    {
        if (permissions.Length == 0)
            throw new ArgumentException("Specify at least one permission.", nameof(permissions));

        _permissions = permissions;
    }

    /// <summary>Accept the caller if they hold <em>any</em> of the listed permissions rather than all of them.</summary>
    public bool RequireAny { get; init; }

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new PermissionRequirement(_permissions, RequireAny);
    }
}

/// <summary>The requirement <see cref="PermissionAuthorizationHandler"/> evaluates.</summary>
public sealed class PermissionRequirement(IReadOnlyList<string> permissions, bool requireAny) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; } = permissions;

    public bool RequireAny { get; } = requireAny;
}
