using Tiki.Shared.Core.Models;

namespace Tiki.Shared.Results;

/// <summary>
/// Maps a <see cref="Result{T}"/> / <see cref="Result"/> onto the uniform
/// <see cref="ApiResponse{T}"/> / <see cref="ApiResponse"/> envelope. Call this at the HTTP
/// boundary only — handlers, gRPC mapping, and Kafka consumers keep working with
/// <see cref="Result{T}"/> directly and never see an envelope.
/// </summary>
public static class ResultExtensions
{
    public static ApiResponse<T> ToApiResponse<T>(this Result<T> result, string? successMessage = null) =>
        result.IsSuccess
            ? ApiResponse<T>.Ok(result.Value, successMessage)
            : ApiResponse<T>.Fail(result.Error.Code, result.Error.Message);

    public static ApiResponse ToApiResponse(this Result result, string? successMessage = null) =>
        result.IsSuccess
            ? ApiResponse.Ok(successMessage)
            : ApiResponse.Fail(result.Error.Code, result.Error.Message);
}