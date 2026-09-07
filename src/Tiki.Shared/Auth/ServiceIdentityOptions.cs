using System.ComponentModel.DataAnnotations;

namespace Tiki.Shared.Auth;

/// <summary>
/// Who this service is in the mesh, and which callers it will accept a signed request from.
///
/// <para>
/// Each service holds its <em>own</em> signing secret and a map of the secrets belonging to
/// the services allowed to call it. The earlier design used one secret shared by every
/// service in the mesh, which meant a single leaked value let an attacker impersonate any
/// service to any other, and rotating it was a synchronised restart of the whole estate.
/// Per-service keys make a compromise blast-radius one service, and rotation a per-pair
/// change.
/// </para>
/// </summary>
public sealed class ServiceIdentityOptions
{
    public const string SectionName = "Tiki:Auth:ServiceIdentity";

    /// <summary>This service's own id, e.g. <c>wallet-service</c>. Signed into every outbound request.</summary>
    [Required]
    public required string ServiceId { get; init; }

    /// <summary>This service's own signing secret, used for outbound calls. At least 32 bytes.</summary>
    [Required, MinLength(32)]
    public required string SigningSecret { get; init; }

    /// <summary>
    /// Secrets of the services permitted to call this one, keyed by their service id.
    /// A caller whose id is absent is rejected before any cryptography runs — this map is
    /// the allow-list as well as the key store.
    /// </summary>
    public Dictionary<string, string> TrustedCallers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How far a request's timestamp may sit from this service's clock in either direction.
    /// It bounds how long a captured request stays replayable if the nonce store is
    /// unavailable, and it has to absorb real clock drift between hosts — 5 minutes is the
    /// usual compromise, and matches AWS SigV4.
    /// </summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Reject a request whose nonce has been seen before. On by default. Turning it off
    /// leaves a <see cref="ClockSkew"/>-wide replay window and should only ever be a
    /// deliberate, temporary response to a nonce-store outage.
    /// </summary>
    public bool RequireNonce { get; init; } = true;
}
