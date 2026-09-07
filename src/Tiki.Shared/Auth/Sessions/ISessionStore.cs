namespace Tiki.Shared.Auth.Sessions;

/// <summary>
/// The session record store. Identity owns the write side (login creates, logout revokes);
/// every other service only ever calls <see cref="GetAsync"/>.
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Records a new session with a TTL taken from <see cref="TikiSession.ExpiresAt"/>, and
    /// indexes it against the user so "log out everywhere" is possible.
    /// </summary>
    Task CreateAsync(TikiSession session, CancellationToken ct = default);

    /// <summary>
    /// The live session, or <see langword="null"/> if it never existed, expired, or was
    /// revoked. A null return is what makes a still-unexpired access token stop working.
    /// </summary>
    Task<TikiSession?> GetAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Ends one session — the logout path. Returns false if it was already gone.</summary>
    Task<bool> RevokeAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Ends every session belonging to a user, and returns how many. The path for "log out
    /// all devices", a password change, and a suspected compromise.
    /// </summary>
    Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Every live session for a user, so they can be listed and revoked individually.</summary>
    Task<IReadOnlyList<TikiSession>> GetUserSessionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Rewrites the access map on every live session of a user, in place, preserving each
    /// session's remaining TTL.
    /// </summary>
    /// <remarks>
    /// This is what makes a membership change take effect on the user's next request rather
    /// than at their next login. Granting someone a new tenant, changing their role, or
    /// revoking their access all land immediately — which matters most in the revoking
    /// direction, where waiting for a re-login is exactly the wrong behaviour.
    /// </remarks>
    Task<int> UpdateAccessAsync(
        Guid userId,
        IReadOnlyList<string> globalPermissions,
        IReadOnlyDictionary<Guid, TenantGrant> tenantAccess,
        CancellationToken ct = default);

    /// <summary>Extends a session's TTL on activity, for a service that wants sliding expiry.</summary>
    Task TouchAsync(Guid sessionId, TimeSpan extendBy, CancellationToken ct = default);
}
