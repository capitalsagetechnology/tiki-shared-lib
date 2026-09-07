namespace Tiki.Shared.Core.Exceptions;

/// <summary>
/// The caller is not authenticated, or the session behind their token is no longer live.
/// Mapped to 401 by <see cref="Middleware.ErrorHandlingMiddleware"/>.
/// </summary>
public sealed class UnauthorizedException(string message = "Authentication is required.", string code = "tiki.unauthorized")
    : TikiException(message, code);
