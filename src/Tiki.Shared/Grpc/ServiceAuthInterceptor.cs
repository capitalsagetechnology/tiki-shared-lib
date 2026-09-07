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
        await AuthenticateAsync(context);
        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await AuthenticateAsync(context);
        await continuation(request, responseStream, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream, ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await AuthenticateAsync(context);
        return await continuation(requestStream, context);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await AuthenticateAsync(context);
        await continuation(requestStream, responseStream, context);
    }

    private async Task AuthenticateAsync(ServerCallContext context)
    {
        var result = await VerifyAsync(context.RequestHeaders, context.Method, verifier, context.CancellationToken);
        if (!result.IsValid)
            throw new RpcException(new Status(StatusCode.Unauthenticated, result.FailureReason ?? "Unauthorized."));

        ServiceContext.CallingService = result.CallingService;

        // Only after the signature verifies — before that, these are attacker-controlled.
        if (Guid.TryParse(context.RequestHeaders.GetValue(TikiHeaderNames.TenantId), out var tenantId))
            ServiceContext.TenantId = tenantId;
        if (Guid.TryParse(context.RequestHeaders.GetValue(TikiHeaderNames.UserId), out var userId))
            ServiceContext.UserId = userId;
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
