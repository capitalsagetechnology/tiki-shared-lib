namespace Tiki.Shared.Core.Models;

/// <summary>The uniform HTTP envelope every service wraps a response in — success or failure alike.</summary>
public sealed record ApiResponse<T>
{
    public required bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message,
        Timestamp = DateTimeOffset.UtcNow,
    };

    public static ApiResponse<T> Fail(string errorCode, string message) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        Message = message,
        Timestamp = DateTimeOffset.UtcNow,
    };
}

/// <summary>The non-generic envelope, for operations with no payload.</summary>
public sealed record ApiResponse
{
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    public static ApiResponse Ok(string? message = null) => new()
    {
        Success = true,
        Message = message,
        Timestamp = DateTimeOffset.UtcNow,
    };

    public static ApiResponse Fail(string errorCode, string message) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        Message = message,
        Timestamp = DateTimeOffset.UtcNow,
    };
}