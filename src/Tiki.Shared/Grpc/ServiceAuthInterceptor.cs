using System.Globalization;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Tiki.Shared.Auth;
using Tiki.Shared.Gateway;

namespace Tiki.Shared.Grpc;

/// <summary>
/// Verifies the signature on every inbound gRPC call. A call that is unsigned, signed by an
/// untrusted service, stale, or replayed is rejected with
/// <see cref="StatusCode.Unauthenticated"/> before it reaches the handler.
/// </summary>
public sealed class ServiceAuthInterceptor(IServiceRequestVerifier verifier) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        (await AuthenticateAsync(context)).Apply();
        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        (await AuthenticateAsync(context)).Apply();
        await continuation(request, responseStream, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream, ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        (await AuthenticateAsync(context)).Apply();
        return await continuation(requestStream, context);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        (await AuthenticateAsync(context)).Apply();
        await continuation(requestStream, responseStream, context);
    }

    /// <summary>
    /// Verifies the call and returns the identity it carries, WITHOUT writing it to
    /// <see cref="ServiceContext"/>.
    /// </summary>
    /// <remarks>
    /// The write is the caller's job, and that is not a style preference. Execution context is
    /// copy-on-write, so an <c>AsyncLocal</c> set inside an awaited method is invisible to the
    /// caller once that method returns — the same behaviour <see cref="AmbientContextMiddleware"/>
    /// exists to work around on the JWT path. Assigning here left every mesh gRPC handler running
    /// with a null tenant, which the EF Core global tenant filter then reads as "no tenant": the
    /// handler saw none of its own tenant's rows and wrote rows belonging to nobody. Returning the
    /// values and letting the handler method assign them before it awaits the continuation puts
    /// the write in a context the handler is downstream of, where it is visible.
    /// </remarks>
    private async Task<MeshIdentity> AuthenticateAsync(ServerCallContext context)
    {
        var result = await VerifyAsync(context.RequestHeaders, context.Method, verifier, context.CancellationToken);
        if (!result.IsValid)
            throw new RpcException(new Status(StatusCode.Unauthenticated, result.FailureReason ?? "Unauthorized."));

        // Only read after the signature verifies — before that, these are attacker-controlled.
        return new MeshIdentity(
            result.CallingService,
            ParseGuid(context.RequestHeaders.GetValue(TikiHeaderNames.TenantId)),
            ParseGuid(context.RequestHeaders.GetValue(TikiHeaderNames.UserId)));
    }

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;

    /// <summary>The verified identity of one inbound mesh call, ready to be made ambient.</summary>
    private readonly record struct MeshIdentity(string? CallingService, Guid? TenantId, Guid? UserId)
    {
        /// <summary>
        /// Called from the interceptor's own handler method, so the write flows to the gRPC
        /// handler downstream of it. A null never overwrites a value already established
        /// upstream — an unsigned header is an absence, not a correction.
        /// </summary>
        public void Apply()
        {
            ServiceContext.CallingService = CallingService;

            if (TenantId is not null)
                ServiceContext.TenantId = TenantId;

            if (UserId is not null)
                ServiceContext.UserId = UserId;
        }
    }

    /// <summary>Verification against bare <see cref="Metadata"/>, so it is unit-testable without a server.</summary>
    internal static Task<ServiceAuthResult> VerifyAsync(
        Metadata headers, string methodFullName, IServiceRequestVerifier verifier, CancellationToken ct)
    {
        var serviceId = headers.GetValue(TikiHeaderNames.ServiceId);
        var nonce = headers.GetValue(TikiHeaderNames.Nonce);
        var value = headers.GetValue(TikiHeaderNames.Signature);
        var timestampRaw = headers.GetValue(TikiHeaderNames.Timestamp);

        if (string.IsNullOrWhiteSpace(serviceId) || string.IsNullOrWhiteSpace(nonce) ||
            string.IsNullOrWhiteSpace(value) ||
            !long.TryParse(timestampRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp))
        {
            return Task.FromResult(ServiceAuthResult.Invalid("Call is not signed."));
        }

        return verifier.VerifyAsync(
            new SignableRequest("POST", methodFullName, QueryString: null, HmacRequestSigner.EmptyBodyHash),
            new RequestSignature(serviceId, timestamp, nonce, value),
            ct);
    }
}
