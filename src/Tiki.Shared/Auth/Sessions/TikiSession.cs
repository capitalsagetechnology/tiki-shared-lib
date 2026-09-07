namespace Tiki.Shared.Auth.Sessions;

/// <summary>
/// Everything a service needs to authorise a request, held in Redis and keyed by the
/// <c>sid</c> claim on the access token.
///
/// <para>
/// The access token stays a thin, short-lived pointer: it proves <em>who</em> minted the
/// session and that it has not expired, and carries almost nothing else. Authority — is the
/// session still live, what may this user actually do — is read from here. That split is
/// what makes logout instant (revoking the session invalidates every token pointing at it,
/// rather than waiting out a token's lifetime) and permission changes immediate (a role
/// change lands on the next request, instead of the next login).
/// </para>
///
/// <para>
/// Redis, not Identity, is the read path. Every authenticated request across every service
/// needs this, so routing it through an Identity gRPC call would make Identity a
/// synchronous dependency of the entire platform and its busiest single component. Identity
/// <em>writes</em> sessions; everyone else reads them straight from Redis.
/// </para>
/// </summary>
public sealed record TikiSession
{
    /// <summary>Matches the <c>sid</c> claim on the access token that points at this session.</summary>
    public required Guid SessionId { get; init; }

    public required Guid UserId { get; init; }

    public required Guid TenantId { get; init; }

    /// <summary><c>Customer</c>, <c>BusinessOwner</c> or <c>TeamMember</c>.</summary>
    public required string UserType { get; init; }

    /// <summary>The business a TeamMember is acting for. Null for a Customer or an owner acting personally.</summary>
    public Guid? BusinessId { get; init; }

    /// <summary>
    /// Effective permissions, already flattened from roles by Identity at login. Stored
    /// resolved rather than as role names so a service never has to expand a role — and so
    /// permission checks stay a set lookup on a value already in memory.
    /// </summary>
    public required IReadOnlyList<string> Permissions { get; init; } = [];

    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>
    /// When the session dies regardless of activity. Mirrored as the Redis key's TTL, so an
    /// abandoned session disappears on its own rather than needing a cleanup job.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Last seen — updated on a sliding refresh, if the service enables one.</summary>
    public DateTimeOffset LastSeenAt { get; init; }

    /// <summary>Opaque device fingerprint, so a user can review and revoke their own sessions.</summary>
    public string? DeviceId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    /// <summary>
    /// Exact, case-sensitive permission match. Wildcards are deliberately not supported:
    /// a <c>wallet:*</c> that quietly grants a <c>wallet:delete</c> added six months later
    /// is how privilege escalation gets shipped by accident.
    /// </summary>
    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.Ordinal);

    public bool HasAnyPermission(IEnumerable<string> permissions) =>
        permissions.Any(HasPermission);

    public bool HasAllPermissions(IEnumerable<string> permissions) =>
        permissions.All(HasPermission);
}
