namespace Tiki.Shared.Core.Exceptions;

/// <summary>Maps to HTTP 409 / gRPC <c>ALREADY_EXISTS</c> or <c>FAILED_PRECONDITION</c>.</summary>
public sealed class ConflictException : TikiException
{
    public ConflictException(string message)
        : base(message, "tiki.conflict")
    {
    }
}
