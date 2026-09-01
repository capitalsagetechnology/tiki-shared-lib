namespace Tiki.Shared.Results;

/// <summary>The kind of failure an <see cref="Error"/> represents — maps 1:1 onto HTTP/gRPC status families.</summary>
public enum ErrorType
{
    Failure = 0,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
}

/// <summary>
/// A structured, typed failure carried by <see cref="Result{T}"/>. Unlike an exception,
/// an <see cref="Error"/> is data — it is returned, not thrown, for any failure path an
/// Application-layer handler expects to happen (not-found, validation, business-rule
/// violations).
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}
