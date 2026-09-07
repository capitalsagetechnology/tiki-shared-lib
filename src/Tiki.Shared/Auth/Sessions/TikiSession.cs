using Tiki.Shared.Auth.Authorization;

namespace Tiki.Shared.Auth.Sessions;

/// <summary>
/// A user's access to one tenant: the role names they hold there and the permissions those
/// roles expand to.
/// </summary>
public sealed record TenantGrant
{
    public required Guid TenantId { get; init; }

    /// <summary>Display only — for a "you are an Admin here" label. Never the basis of a check.</summary>
    public required IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>Already expanded (<c>Write</c> implies <c>Read</c>) by Identity at grant time.</summary>
    public required IReadOnlyList<string> Permissions { get; init; } = [];
}

/// <summary>
/// Everything a service needs to authorise a request, held in Redis and keyed by the
/// <c>sid</c> claim on the access token.
///
/// <para>
/// The access token stays a thin, short-lived pointer: it proves who minted the session and
/// that it has not expired. Authority — is the session still live, which tenants may this
/// person touch, what may they do in each — is read from here. That split is what makes logout
/// instant and permission changes immediate, neither of which a self-contained JWT can do.
/// </para>
///
/// <para>
/// Redis, not Identity, is the read path. Every authenticated request across every service
/// needs this, so routing it through Identity would make Identity a synchronous dependency of
/// the whole platform. Identity <em>writes</em> sessions; everyone else reads them.
/// </para>
/// </summary>
public sealed record TikiSession
{
    /// <summary>Matches the <c>sid</c> claim on the access token that points at this session.</summary>
    public required Guid SessionId { get; init; }

    public required Guid UserId { get; init; }

    /// <summary><c>Customer</c>, <c>BusinessOwner</c>, <c>TeamMember</c>.</summary>
    public required string UserType { get; init; }

    /// <summary>
    /// Permissions held at global scope — they apply in <em>every</em> tenant, including ones
    /// created after this session began.
    /// </summary>
    /// <remarks>
    /// This is what "Global Admin" means mechanically. It is deliberately a separate list from
    /// <see cref="TenantAccess"/> rather than being copied into every tenant's entry: copying
    /// would mean a global admin's session had to be rewritten every time a tenant was created,
    /// and a session that missed that rewrite would silently lose access to the new tenant.
    /// </remarks>
    public IReadOnlyList<string> GlobalPermissions { get; init; } = [];

    /// <summary>
    /// Per-tenant access, keyed by tenant id. A user may hold different roles in different
    /// tenants — an Admin in one, a read-only viewer in another — so this cannot collapse to a
    /// single permission list.
    /// </summary>
    public IReadOnlyDictionary<Guid, TenantGrant> TenantAccess { get; init; } =
        new Dictionary<Guid, TenantGrant>();

    /// <summary>
    /// The tenant this user belongs to, for an end user who has exactly one — a customer or a
    /// business owner. Null for platform staff, whose access comes from memberships instead.
    /// Used as the default active tenant when a client selects none.
    /// </summary>
    public Guid? HomeTenantId { get; init; }

    /// <summary>The business a TeamMember is acting for, when applicable.</summary>
    public Guid? BusinessId { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>
    /// When the session dies regardless of activity. Mirrored as the Redis key's TTL, so an
    /// abandoned session disappears on its own rather than needing a cleanup job.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset LastSeenAt { get; init; }

    /// <summary>Opaque device fingerprint, so a user can review and revoke their own sessions.</summary>
    public string? DeviceId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    /// <summary>True if this user holds any global-scope grant.</summary>
    public bool IsGlobal => GlobalPermissions.Count > 0;

    /// <summary>Every tenant this session may operate in.</summary>
    public IReadOnlyCollection<Guid> AccessibleTenantIds => [.. TenantAccess.Keys];

    /// <summary>
    /// Whether this session may operate in a tenant at all.
    /// </summary>
    /// <remarks>
    /// A global grant answers yes for every tenant, including tenants created after this
    /// session started — which is why a global admin does not need re-authentication to
    /// administer a tenant that did not exist when they logged in.
    /// </remarks>
    public bool CanAccessTenant(Guid tenantId) => IsGlobal || TenantAccess.ContainsKey(tenantId);

    /// <summary>
    /// The permissions in force for a request operating in <paramref name="tenantId"/>: global
    /// grants, plus whatever this user holds in that specific tenant.
    /// </summary>
    public IReadOnlyList<string> EffectivePermissions(Guid? tenantId)
    {
        if (tenantId is not { } id || !TenantAccess.TryGetValue(id, out var grant))
            return GlobalPermissions;

        if (GlobalPermissions.Count == 0)
            return grant.Permissions;

        var combined = new HashSet<string>(GlobalPermissions, StringComparer.Ordinal);
        combined.UnionWith(grant.Permissions);
        return [.. combined];
    }

    /// <summary>Whether this session holds <paramref name="permission"/> while operating in a given tenant.</summary>
    public bool HasPermission(string permission, Guid? tenantId) =>
        EffectivePermissions(tenantId).Contains(permission, StringComparer.Ordinal);

    /// <summary>Strongly-typed overload — the form call sites should prefer.</summary>
    public bool HasPermission(TikiModule module, PermissionAction action, Guid? tenantId) =>
        HasPermission(TikiPermission.Format(module, action), tenantId);

    /// <summary>Whether this session holds every one of <paramref name="permissions"/> in the given tenant.</summary>
    public bool HasAllPermissions(IEnumerable<string> permissions, Guid? tenantId)
    {
        var effective = EffectivePermissions(tenantId);
        return permissions.All(p => effective.Contains(p, StringComparer.Ordinal));
    }

    public bool HasAnyPermission(IEnumerable<string> permissions, Guid? tenantId)
    {
        var effective = EffectivePermissions(tenantId);
        return permissions.Any(p => effective.Contains(p, StringComparer.Ordinal));
    }
}
