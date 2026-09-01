namespace Tiki.Shared.Core.Exceptions;

/// <summary>Maps to HTTP 400 / gRPC <c>INVALID_ARGUMENT</c>.</summary>
public sealed class ValidationException : TikiException
{
    /// <summary>Property name → failure messages, matching FluentValidation's shape.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", "tiki.validation")
    {
        Errors = errors;
    }

    public ValidationException(string propertyName, string message)
        : this(new Dictionary<string, string[]> { [propertyName] = [message] })
    {
    }
}
