namespace Tiki.Shared.Gateway;

/// <summary>
/// The wire contract between the API gateway, the services behind it, and the services
/// calling each other. Every one of these names exists in exactly one place so a rename
/// is a compile error rather than a silently-unauthenticated request.
///
/// <para>
/// Two trust zones. <b>Client-supplied</b> headers arrive from the public internet and are
/// never trusted: the gateway strips every header in <see cref="StrippedFromClient"/>
/// before proxying, so a caller cannot assert its own tenant or user id. <b>Mesh</b>
/// headers are stamped by the gateway (or by a calling service) and are trusted only
/// because the request carrying them is HMAC-signed over those same values — see
/// <see cref="Auth.HmacRequestSigner"/>.
/// </para>
/// </summary>
public static class TikiHeaderNames
{
    // --- Correlation -----------------------------------------------------------------

    /// <summary>Trace id for the whole call chain. Accepted from a client (it is not a security decision) and minted when absent.</summary>
    public const string CorrelationId = "X-Correlation-Id";

    /// <summary>One id per inbound gateway request, shared by every downstream call it causes.</summary>
    public const string SessionId = "X-Session-Id";

    // --- Forwarded identity (gateway ➜ service) ---------------------------------------

    /// <summary>
    /// The tenant this request operates in, stamped by the gateway <em>after</em> confirming
    /// the session may access it. Trusted; always in <see cref="StrippedFromClient"/>.
    /// </summary>
    public const string TenantId = "X-Tenant-Id";

    /// <summary>Authenticated end user's id, resolved by the gateway from the validated JWT.</summary>
    public const string UserId = "X-User-Id";

    /// <summary><c>Customer</c>, <c>BusinessOwner</c> or <c>TeamMember</c>, from the validated JWT.</summary>
    public const string UserType = "X-User-Type";

    /// <summary>The end user's session/token id (<c>jti</c>), so a service can check revocation.</summary>
    public const string AuthSessionId = "X-Auth-Session-Id";

    // --- Service-to-service authentication --------------------------------------------

    /// <summary>Id of the service that signed this request, e.g. <c>api-gateway</c>, <c>wallet-service</c>.</summary>
    public const string ServiceId = "X-Tiki-Service-Id";

    /// <summary>Unix seconds at which the request was signed. Outside the skew window, the request is rejected.</summary>
    public const string Timestamp = "X-Tiki-Timestamp";

    /// <summary>Single-use random value. A replayed request presents a nonce already seen and is rejected.</summary>
    public const string Nonce = "X-Tiki-Nonce";

    /// <summary><c>v1=&lt;base64url&gt;</c> — HMAC-SHA256 over the canonical request.</summary>
    public const string Signature = "X-Tiki-Signature";

    // --- Tenant selection (client ➜ gateway) -------------------------------------------

    /// <summary>
    /// A client asking to operate in a particular tenant. Untrusted, and deliberately a
    /// <em>different</em> header from <see cref="TenantId"/>.
    /// </summary>
    /// <remarks>
    /// A user may now hold access to several tenants, so something has to choose which one a
    /// request acts in — and only the client knows. The obvious shortcut is to let the client
    /// send <c>X-Tenant-Id</c> directly and have the gateway validate it, but that would mean
    /// removing <c>X-Tenant-Id</c> from the strip list, and every service behind the gateway
    /// treats that header as proven. One missed validation path and a client is reading
    /// another tenant's data again.
    ///
    /// <para>
    /// Two names keeps the distinction structural rather than procedural: this one is a
    /// request, <see cref="TenantId"/> is a verdict. The gateway reads this, checks it against
    /// the session's access map, and stamps the other.
    /// </para>
    /// </remarks>
    public const string SelectTenant = "X-Tiki-Select-Tenant";

    // --- Client ------------------------------------------------------------------------

    /// <summary>Client-supplied key making a non-idempotent operation safe to retry.</summary>
    public const string IdempotencyKey = "Idempotency-Key";

    /// <summary>Originating client IP, stamped by the gateway from its own connection view.</summary>
    public const string ForwardedFor = "X-Forwarded-For";

    /// <summary>
    /// Headers the gateway removes from every inbound client request before proxying.
    /// Without this, a client could simply send <c>X-Tenant-Id</c> and read another
    /// tenant's data — the services behind the gateway trust these values.
    /// </summary>
    public static readonly string[] StrippedFromClient =
    [
        TenantId,
        UserId,
        UserType,
        AuthSessionId,
        ServiceId,
        Timestamp,
        Nonce,
        Signature,
        SessionId,

        // The gateway consumes the client's selection and stamps the verified TenantId above,
        // so nothing downstream should see the raw request — a service that read this instead
        // of the stamped header would be trusting client input again.
        SelectTenant,
    ];
}
