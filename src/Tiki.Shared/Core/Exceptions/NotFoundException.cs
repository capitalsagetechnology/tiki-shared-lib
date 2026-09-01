namespace Tiki.Shared.Core.Exceptions;

/// <summary>Maps to HTTP 404 / gRPC <c>NOT_FOUND</c>.</summary>
public sealed class NotFoundException : TikiException
{
    public NotFoundException(string message)
        : base(message, "tiki.not_found")
    {
    }

    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.", "tiki.not_found")
    {
    }
}
