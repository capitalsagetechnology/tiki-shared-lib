using Microsoft.AspNetCore.Authorization;

namespace Tiki.Shared.Auth.Authorization;

/// <summary>
/// Requires the caller's session to hold a permission on a module, in the tenant the request
/// is operating in.
///
/// <code>
/// [RequiresPermission(TikiModule.Tenants, PermissionAction.Write)]
/// public Task&lt;ActionResult&gt; Create(...) { }
/// </code>
///
/// <para>
/// Declaring the requirement on the endpoint rather than writing an <c>if</c> in the handler is
/// what makes the platform auditable: every permission the system enforces can be enumerated
/// from its routes, which is the question an auditor actually asks and the one a scatter of
/// inline checks cannot answer.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresPermissionAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    private readonly string[] _permissions;

    /// <summary>Requires one module/action pair.</summary>
    public RequiresPermissionAttribute(TikiModule module, PermissionAction action)
        : this([new TikiPermission(module, action)])
    {
    }

    /// <summary>
    /// Requires an action across several modules — ANDed by default, see <see cref="RequireAny"/>.
    /// </summary>
    public RequiresPermissionAttribute(PermissionAction action, params TikiModule[] modules)
        : this([.. modules.Select(m => new TikiPermission(m, action))])
    {
    }

    private RequiresPermissionAttribute(TikiPermission[] permissions)
    {
        if (permissions.Length == 0)
            throw new ArgumentException("Specify at least one permission.", nameof(permissions));

        // Stored in the canonical string form the session holds, so the check at request time
        // is a set lookup with no parsing or formatting on the hot path.
        _permissions = [.. permissions.Select(p => p.Value)];
    }

    /// <summary>Accept the caller if they hold <em>any</em> of the listed permissions rather than all.</summary>
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
