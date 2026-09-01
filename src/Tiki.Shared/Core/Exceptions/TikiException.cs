namespace Tiki.Shared.Core.Exceptions;

/// <summary>
/// Root of the shared exception hierarchy. Thrown for failures that are unexpected —
/// expected failure paths (not-found, validation, business-rule violations) should
/// return <see cref="Results.Result{T}"/> instead of throwing. <see cref="Middleware.ErrorHandlingMiddleware"/>
/// maps this hierarchy to <c>ProblemDetails</c> with zero per-service mapping code.
/// </summary>
public class TikiException : Exception
{
    /// <summary>Machine-readable error code surfaced in the <c>ProblemDetails</c> body.</summary>
    public string Code { get; }

    public TikiException(string message, string code = "tiki.error")
        : base(message)
    {
        Code = code;
    }

    public TikiException(string message, Exception innerException, string code = "tiki.error")
        : base(message, innerException)
    {
        Code = code;
    }
}
