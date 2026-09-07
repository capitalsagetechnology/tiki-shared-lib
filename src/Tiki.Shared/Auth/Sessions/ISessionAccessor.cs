namespace Tiki.Shared.Auth.Sessions;

/// <summary>
/// The current request's session, already fetched and validated at authentication time.
/// Inject this anywhere in the request's call graph to read the caller's tenant, user, or
/// permissions without another Redis round trip — the lookup happened once, at the edge.
/// </summary>
public interface ISessionAccessor
{
    /// <summary>The current session, or <see langword="null"/> on an unauthenticated request.</summary>
    TikiSession? Session { get; }

    /// <summary>
    /// The tenant this request is operating in — a user with access to several tenants acts in
    /// exactly one per request.
    /// </summary>
    /// <remarks>
    /// Read from <see cref="ServiceContext.TenantId"/>, which is only ever written from a
    /// validated source: the gateway stamps it after checking the session grants access, or an
    /// inter-service request carries it under an HMAC signature. Never from a client header.
    /// </remarks>
    Guid? ActiveTenantId { get; }

    /// <summary>The current session, or a 401-shaped exception if there is none.</summary>
    TikiSession Require();

    /// <summary>
    /// The active tenant, or a 400-shaped exception if the request did not select one.
    /// </summary>
    /// <remarks>
    /// For a handler that is meaningless without a tenant. A user with access to three tenants
    /// who selects none must be told to choose, not silently served the first one — picking on
    /// their behalf is how someone edits the wrong tenant's settings.
    /// </remarks>
    Guid RequireTenant();
}

/// <summary>
/// Reads the session out of <c>HttpContext.Items</c>, where
/// <c>AddTikiJwtAuth</c>'s <c>OnTokenValidated</c> handler put it.
/// </summary>
public sealed class HttpContextSessionAccessor(Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) : ISessionAccessor
{
    /// <summary>The <c>HttpContext.Items</c> key the JWT event writes the session under.</summary>
    public const string ItemsKey = "tiki.session";

    public TikiSession? Session =>
        accessor.HttpContext?.Items.TryGetValue(ItemsKey, out var value) == true ? value as TikiSession : null;

    public Guid? ActiveTenantId => ServiceContext.TenantId;

    public TikiSession Require() =>
        Session ?? throw new Core.Exceptions.UnauthorizedException(
            "This operation requires an authenticated session.");

    public Guid RequireTenant() =>
        ActiveTenantId ?? throw new Core.Exceptions.TikiException(
            $"This operation requires a tenant. Send the '{Gateway.TikiHeaderNames.SelectTenant}' header to choose one.",
            "Auth.NoActiveTenant");
}
