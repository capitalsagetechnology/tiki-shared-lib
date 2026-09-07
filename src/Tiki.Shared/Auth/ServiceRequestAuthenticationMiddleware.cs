using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tiki.Shared.Core.Attributes;
using Tiki.Shared.Gateway;

namespace Tiki.Shared.Auth;

/// <summary>
/// Rejects an inbound HTTP request carrying <see cref="RequireServiceTokenAttribute"/>
/// unless it is correctly signed by a trusted service. On success, populates
/// <see cref="ServiceContext.CallingService"/> and the forwarded identity ambient values.
/// </summary>
public sealed class ServiceRequestAuthenticationMiddleware(
    RequestDelegate next,
    ILogger<ServiceRequestAuthenticationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IServiceRequestVerifier verifier)
    {
        var requirement = context.GetEndpoint()?.Metadata.GetMetadata<RequireServiceTokenAttribute>();
        if (requirement is null)
        {
            await next(context);
            return;
        }

        if (!TryReadSignature(context.Request, out var signature))
        {
            await WriteProblem(context, StatusCodes.Status401Unauthorized, "Request is not signed.");
            return;
        }

        // The body is part of what was signed, so it has to be read to hash it — and then
        // read again by the model binder. EnableBuffering makes the stream rewindable so
        // the second read sees the same bytes; without it the handler would get an empty body.
        context.Request.EnableBuffering();
        var bodyHash = await HmacRequestSigner.HashBodyAsync(context.Request.Body, context.RequestAborted);
        context.Request.Body.Position = 0;

        var request = new SignableRequest(
            context.Request.Method,
            context.Request.Path.Value ?? "/",
            context.Request.QueryString.Value,
            bodyHash);

        var result = await verifier.VerifyAsync(request, signature, context.RequestAborted);
        if (!result.IsValid)
        {
            await WriteProblem(context, StatusCodes.Status401Unauthorized, result.FailureReason ?? "Unauthorized.");
            return;
        }

        if (requirement.AllowedCallers.Length > 0 &&
            !requirement.AllowedCallers.Contains(result.CallingService, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Service {CallingService} is authenticated but not on the allow-list for {Path}.",
                result.CallingService, context.Request.Path);
            await WriteProblem(context, StatusCodes.Status403Forbidden, "Caller is not permitted to access this endpoint.");
            return;
        }

        ServiceContext.CallingService = result.CallingService;

        // Only now — after the signature covering these very headers has verified — is it
        // safe to believe the forwarded identity. Reading them before verification would
        // mean trusting whatever an unauthenticated caller chose to send.
        ApplyForwardedIdentity(context);

        await next(context);
    }

    /// <summary>Reads the four signature headers, treating any missing one as "unsigned".</summary>
    internal static bool TryReadSignature(HttpRequest request, out RequestSignature signature)
    {
        signature = default;

        var serviceId = request.Headers[TikiHeaderNames.ServiceId].FirstOrDefault();
        var timestampRaw = request.Headers[TikiHeaderNames.Timestamp].FirstOrDefault();
        var nonce = request.Headers[TikiHeaderNames.Nonce].FirstOrDefault();
        var value = request.Headers[TikiHeaderNames.Signature].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(serviceId) || string.IsNullOrWhiteSpace(nonce) ||
            string.IsNullOrWhiteSpace(value) ||
            !long.TryParse(timestampRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp))
        {
            return false;
        }

        signature = new RequestSignature(serviceId, timestamp, nonce, value);
        return true;
    }

    private static void ApplyForwardedIdentity(HttpContext context)
    {
        if (Guid.TryParse(context.Request.Headers[TikiHeaderNames.TenantId], out var tenantId))
            ServiceContext.TenantId = tenantId;

        if (Guid.TryParse(context.Request.Headers[TikiHeaderNames.UserId], out var userId))
            ServiceContext.UserId = userId;
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
