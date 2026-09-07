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

    /// <summary>The <c>HttpContext.Items</c> key the JWT event writes the resolved active tenant under.</summary>
    /// <remarks>
    /// Carried on <c>HttpContext.Items</c> rather than only on <see cref="ServiceContext"/>
    /// because of how <see cref="AsyncLocal{T}"/> actually behaves: a write inside the JWT
    /// handler happens in a nested execution context, and execution context is copy-on-write,
    /// so the value is <b>not</b> visible to the middleware pipeline after that await returns.
    /// <c>HttpContext.Items</c> is a plain shared dictionary and has no such semantics.
    /// <c>UseTikiAmbientContext()</c> then copies it onto <see cref="ServiceContext"/> from a
    /// middleware, where a write before <c>await next()</c> does flow to everything downstream.
    /// </remarks>
    public const string ActiveTenantItemsKey = "tiki.active-tenant";

    public TikiSession? Session =>
        accessor.HttpContext?.Items.TryGetValue(ItemsKey, out var value) == true ? value as TikiSession : null;

    public Guid? ActiveTenantId =>
        accessor.HttpContext?.Items.TryGetValue(ActiveTenantItemsKey, out var value) == true && value is Guid tenantId
            ? tenantId
            // Falls back to the ambient value for call paths with no HttpContext at all — a
            // Kafka consumer handling a message, most obviously.
            : ServiceContext.TenantId;

    public TikiSession Require() =>
        Session ?? throw new Core.Exceptions.UnauthorizedException(
            "This operation requires an authenticated session.");

    public Guid RequireTenant() =>
        ActiveTenantId ?? throw new Core.Exceptions.TikiException(
            $"This operation requires a tenant. Send the '{Gateway.TikiHeaderNames.SelectTenant}' header to choose one.",
            "Auth.NoActiveTenant");
}
