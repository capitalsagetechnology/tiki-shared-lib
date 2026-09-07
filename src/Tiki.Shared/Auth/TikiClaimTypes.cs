namespace Tiki.Shared.Auth;

/// <summary>
/// Claim names on the end-user access token Identity issues. Short, non-URI names —
/// <see cref="TikiJwtExtensions.AddTikiJwtAuth"/> disables .NET's inbound claim-type map
/// so what Identity wrote is what a service reads, rather than the SOAP-era URIs the
/// default mapping rewrites them to.
/// </summary>
public static class TikiClaimTypes
{
    /// <summary>The user's id (GUID).</summary>
    public const string UserId = "sub";

    /// <summary>The tenant the user belongs to (GUID). Drives the EF Core tenant query filter on every service.</summary>
    public const string TenantId = "tenant_id";

    /// <summary><c>Customer</c>, <c>BusinessOwner</c> or <c>TeamMember</c>.</summary>
    public const string UserType = "user_type";

    /// <summary>Unique id of this individual token. Useful for tracing a single token; not the revocation key.</summary>
    public const string TokenId = "jti";

    /// <summary>
    /// The session this token points at. The revocation key: services look the session up
    /// in Redis on every request, so deleting it logs the user out everywhere at once —
    /// which a signed JWT, on its own, can never be made to do.
    /// </summary>
    public const string SessionId = "sid";

    /// <summary>The business a TeamMember is acting on behalf of, when applicable.</summary>
    public const string BusinessId = "business_id";

    public const string Email = "email";
    public const string EmailVerified = "email_verified";
    public const string PhoneNumber = "phone_number";
    public const string PhoneNumberVerified = "phone_number_verified";

    /// <summary>Granted permissions, one claim per value.</summary>
    public const string Permission = "permission";

    /// <summary><c>access</c> or <c>refresh</c> — a refresh token presented as a bearer token is rejected on type alone.</summary>
    public const string TokenType = "token_type";
}
