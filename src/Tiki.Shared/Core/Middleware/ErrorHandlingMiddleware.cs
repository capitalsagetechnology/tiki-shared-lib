using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Core.Exceptions;

namespace Tiki.Shared.Core.Middleware;

/// <summary>
/// Maps any exception in the <see cref="TikiException"/> hierarchy — and, as a fallback,
/// any unhandled exception — to a <see cref="ProblemDetails"/> response. Throwing a
/// hierarchy exception from a controller returns the correct HTTP status with a
/// structured body, with zero per-service mapping code.
/// </summary>
public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var problem = Map(ex);

            if (problem.Status >= StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "Unhandled exception mapped to {Status}", problem.Status);
            else
                logger.LogWarning(ex, "Request failed with {Status}: {Detail}", problem.Status, problem.Detail);

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static ProblemDetails Map(Exception exception) => exception switch
    {
        Exceptions.ValidationException validation => new ValidationProblemDetails(
            validation.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
        {
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Type = $"tiki-error:{validation.Code}",
        },
        NotFoundException notFound => new ProblemDetails
        {
            Title = "Resource not found.",
            Detail = notFound.Message,
            Status = StatusCodes.Status404NotFound,
            Type = $"tiki-error:{notFound.Code}",
        },
        ConflictException conflict => new ProblemDetails
        {
            Title = "Request conflicts with current state.",
            Detail = conflict.Message,
            Status = StatusCodes.Status409Conflict,
            Type = $"tiki-error:{conflict.Code}",
        },
        TikiException tiki => new ProblemDetails
        {
            Title = "Request could not be completed.",
            Detail = tiki.Message,
            Status = StatusCodes.Status400BadRequest,
            Type = $"tiki-error:{tiki.Code}",
        },
        _ => new ProblemDetails
        {
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Type = "tiki-error:unhandled",
        },
    };
}
