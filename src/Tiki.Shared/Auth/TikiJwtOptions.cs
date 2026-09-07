using System.ComponentModel.DataAnnotations;

namespace Tiki.Shared.Auth;

/// <summary>
/// How this service validates the end-user access token Identity issues.
///
/// <para>
/// Every service was previously calling <c>AddJwtBearer</c> by hand, and the copies had
/// already drifted: Identity's own registration set <c>ValidateIssuer = false</c> and
/// <c>ValidateAudience = false</c>, which means a token minted by any system holding the
/// same signing key — a staging environment, an unrelated internal tool — would have been
/// accepted as a production Tiki user. Validation lives here now, and issuer and audience
/// are required rather than optional.
/// </para>
/// </summary>
public sealed class TikiJwtOptions
{
    public const string SectionName = "Tiki:Auth:Jwt";

    /// <summary>Expected <c>iss</c>. Required — a token from another issuer is not this platform's user.</summary>
    [Required]
    public required string Issuer { get; init; }

    /// <summary>Expected <c>aud</c>. Required — it is what stops a token minted for a different API being replayed here.</summary>
    [Required]
    public required string Audience { get; init; }

    /// <summary>
    /// Symmetric signing key, for the interim HS256 tokens Identity issues today.
    /// Mutually exclusive with <see cref="JwksUri"/> — set exactly one.
    /// </summary>
    [MinLength(32)]
    public string? SigningKey { get; init; }

    /// <summary>
    /// JWKS endpoint, for the asymmetric (RS256/ES256) tokens Identity moves to. Preferred:
    /// with it, a service verifies with a public key and never holds a value capable of
    /// <em>minting</em> a token, which a shared symmetric key inherently is.
    /// </summary>
    public string? JwksUri { get; init; }

    /// <summary>
    /// Tolerance for clock drift between Identity and this service when checking <c>exp</c>
    /// and <c>nbf</c>. Deliberately small: .NET's default is a surprising 5 minutes, which
    /// keeps a revoked or expired token working for five minutes longer than its own
    /// expiry claims.
    /// </summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromSeconds(30);
}
