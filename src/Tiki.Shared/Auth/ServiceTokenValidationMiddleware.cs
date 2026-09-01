using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Core.Attributes;

namespace Tiki.Shared.Auth;

/// <summary>
/// Validates the inter-service token on any endpoint carrying <see cref="RequireServiceTokenAttribute"/>,
/// rejecting an inbound call before it reaches the handler if the token is missing, invalid,
/// or not from an allowed caller. Populates <see cref="ServiceContext.CallingService"/> on success.
/// </summary>
public sealed class ServiceTokenValidationMiddleware(
    RequestDelegate next,
    IServiceTokenProvider tokenProvider,
    ILogger<ServiceTokenValidationMiddleware> logger)
{
    public const string TokenHeaderName = "X-Service-Token";
    public const string CallingServiceHeaderName = "X-Calling-Service";

    public async Task InvokeAsync(HttpContext context)
    {
        var requireToken = context.GetEndpoint()?.Metadata.GetMetadata<RequireServiceTokenAttribute>();
        if (requireToken is null)
        {
            await next(context);
            return;
        }

        var token = context.Request.Headers[TokenHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            await WriteProblem(context, StatusCodes.Status401Unauthorized, "Missing service token.");
            return;
        }

        var result = await tokenProvider.ValidateAsync(token, context.RequestAborted);
        if (!result.IsValid)
        {
            logger.LogWarning("Service token rejected: {Reason}", result.FailureReason);
            await WriteProblem(context, StatusCodes.Status401Unauthorized, result.FailureReason ?? "Invalid service token.");
            return;
        }

        if (requireToken.AllowedCallers.Length > 0 &&
            !requireToken.AllowedCallers.Contains(result.CallingService, StringComparer.OrdinalIgnoreCase))
        {
            await WriteProblem(context, StatusCodes.Status403Forbidden, "Calling service is not permitted to access this endpoint.");
            return;
        }

        ServiceContext.CallingService = result.CallingService;
        context.Request.Headers[CallingServiceHeaderName] = result.CallingService;
        await next(context);
    }

    private static Task WriteProblem(HttpContext context, int status, string detail)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = status == StatusCodes.Status401Unauthorized ? "Unauthorized" : "Forbidden",
            Detail = detail,
            Status = status,
        });
    }
}
