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

    /// <summary>The current session, or a 401-shaped exception if there is none.</summary>
    TikiSession Require();
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

    public TikiSession Require() =>
        Session ?? throw new Core.Exceptions.UnauthorizedException(
            "This operation requires an authenticated session.");
}
