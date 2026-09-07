namespace Tiki.Shared.Core.Exceptions;

/// <summary>
/// The caller is authenticated but lacks the permission this operation requires.
/// Distinct from <see cref="UnauthorizedException"/> on purpose: 401 tells a client to
/// re-authenticate, 403 tells it not to bother. Mapped to 403 by
/// <see cref="Middleware.ErrorHandlingMiddleware"/>.
/// </summary>
public sealed class ForbiddenException(string message = "You do not have permission to perform this action.", string code = "tiki.forbidden")
    : TikiException(message, code);
