namespace Tiki.Shared.Auth.Authorization;

/// <summary>
/// The platform's modules — the axis a role's permission grid is built along. A role grants
/// <see cref="PermissionAction.Read"/> or <see cref="PermissionAction.Write"/> on each of
/// these, which is what determines both what a user can do and what a UI shows them.
/// </summary>
/// <remarks>
/// This lives in <c>Tiki.Shared</c>, and it is the one deliberate exception to "no domain
/// vocabulary here" — the same exception the permission strings have always been. A module
/// name is a contract between the service that <em>grants</em> it (Identity, building a
/// session) and every service that <em>enforces</em> it. Left in each repo, the grant side
/// writes <c>wallets</c> and the enforce side writes <c>wallet</c>, and the mismatch shows up
/// as a 403 nobody can explain rather than as a build error.
///
/// <para>
/// An enum rather than free text, for the same reason: a role that grants a module which no
/// longer exists — or never did, because someone typo'd it into the database — is invisible
/// until a user is mysteriously denied.
/// </para>
/// </remarks>
public enum TikiModule
{
    /// <summary>Tenants themselves. <c>Write</c> here is what creating a tenant requires.</summary>
    Tenants,

    /// <summary>End users — customers and business owners.</summary>
    Users,

    /// <summary>Role definitions and their permission grids.</summary>
    Roles,

    /// <summary>Platform team membership: who belongs to the global team or to a tenant's team.</summary>
    Teams,

    /// <summary>Businesses onboarded onto the platform, and their own team members.</summary>
    Businesses,

    Wallets,

    Transactions,

    Compliance,

    /// <summary>Third-party provider configuration and the calls made through it.</summary>
    Integrations,

    /// <summary>Reporting and analytics surfaces.</summary>
    Reports,

    /// <summary>Platform and tenant configuration.</summary>
    Settings,
}

/// <summary>
/// What a role may do within a module.
/// </summary>
/// <remarks>
/// Deliberately two values, not a long verb list. A role editor is then a grid — modules down,
/// read/write across — which is a thing a person can hold in their head and audit at a glance;
/// a per-endpoint verb list is not.
///
/// <para>
/// Operations that genuinely need a third level of authority — approving a payout, freezing a
/// wallet, overriding a compliance decision — are <em>not</em> modelled as extra actions here.
/// They are separate approval workflows with their own records, because "who approved this
/// payout" needs an audit trail with a timestamp and a reason, which a permission bit cannot
/// carry. Adding a value to this enum is possible and additive, but reach for it only when the
/// answer really is a capability rather than a recorded decision.
/// </para>
/// </remarks>
public enum PermissionAction
{
    /// <summary>See the module and its data.</summary>
    Read = 1,

    /// <summary>
    /// Create, change and remove within the module. Implies <see cref="Read"/> — see
    /// <see cref="TikiPermission.Expand"/>. Granting write without read produces a user who
    /// can edit a thing they cannot see, which is never what anyone meant.
    /// </summary>
    Write = 2,
}
