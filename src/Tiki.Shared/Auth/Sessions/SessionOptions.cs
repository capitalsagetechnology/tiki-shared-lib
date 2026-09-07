namespace Tiki.Shared.Auth.Sessions;

/// <summary>Options for the Redis session store and the per-request session check.</summary>
public sealed class SessionOptions
{
    public const string SectionName = "Tiki:Auth:Sessions";

    /// <summary>
    /// How long a session stays valid without a refresh. Matches the refresh-token
    /// lifetime, because the session <em>is</em> what the refresh token refreshes.
    /// </summary>
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromDays(30);

    /// <summary>
    /// How long a session is held in this process's memory before Redis is consulted again.
    ///
    /// <para>
    /// This is the one real trade-off in the design, and it is worth being explicit about:
    /// a logout takes effect everywhere after at most this long, because a replica that
    /// cached the session a moment earlier will keep honouring it until the entry ages out.
    /// Five seconds keeps Redis off the hot path for a burst of requests from one user
    /// while keeping the revocation window short enough to be uninteresting to an attacker.
    /// Set it to <see cref="TimeSpan.Zero"/> for strictly-immediate revocation, at the cost
    /// of one Redis round trip per authenticated request.
    /// </para>
    /// </summary>
    public TimeSpan CacheWindow { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Extend a session's expiry on activity. Off by default: sliding expiry means a
    /// compromised session that keeps being used never expires on its own.
    /// </summary>
    public bool SlidingExpiration { get; init; }

    /// <summary>Concurrent sessions per user before the oldest is evicted. Zero disables the cap.</summary>
    public int MaxSessionsPerUser { get; init; } = 10;

    /// <summary>
    /// How long a user's session-index key survives. Longer than <see cref="Lifetime"/>, so
    /// the index never expires out from under a session it still points at.
    /// </summary>
    public TimeSpan UserIndexRetention { get; init; } = TimeSpan.FromDays(45);
}
