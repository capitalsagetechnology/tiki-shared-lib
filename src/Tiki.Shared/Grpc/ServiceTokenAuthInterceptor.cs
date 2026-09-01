using Grpc.Core;
using Grpc.Core.Interceptors;
using Tiki.Shared.Auth;

namespace Tiki.Shared.Grpc;

/// <summary>
/// Validates the inter-service token on every inbound gRPC call for whichever service
/// registers this interceptor (via <c>AddGrpc().AddServiceOptions&lt;T&gt;(o =&gt;
/// o.Interceptors.Add(...))</c>) — a call missing a valid token is rejected with
/// <see cref="StatusCode.Unauthenticated"/> before it reaches the handler. On success,
/// populates <see cref="ServiceContext.CallingService"/> for the lifetime of the call.
/// </summary>
public sealed class ServiceTokenAuthInterceptor(IServiceTokenProvider tokenProvider) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAsync(context);
        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAsync(context);
        await continuation(request, responseStream, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream, ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAsync(context);
        return await continuation(requestStream, context);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAsync(context);
        await continuation(requestStream, responseStream, context);
    }

    private async Task ValidateAsync(ServerCallContext context)
    {
        var result = await ValidateTokenAsync(context.RequestHeaders, tokenProvider, context.CancellationToken);
        if (!result.IsValid)
            throw new RpcException(new Status(StatusCode.Unauthenticated, result.FailureReason ?? "Invalid service token."));

        ServiceContext.CallingService = result.CallingService;
    }

    /// <summary>The token-check logic on its own, independent of <see cref="ServerCallContext"/> — easy to unit test with just a <see cref="Metadata"/> instance.</summary>
    internal static Task<ServiceTokenValidationResult> ValidateTokenAsync(
        Metadata requestHeaders, IServiceTokenProvider tokenProvider, CancellationToken ct)
    {
        var token = requestHeaders.GetValue(ServiceTokenClientInterceptor.TokenMetadataKey);
        return string.IsNullOrWhiteSpace(token)
            ? Task.FromResult(ServiceTokenValidationResult.Invalid("Missing service token."))
            : tokenProvider.ValidateAsync(token, ct);
    }
}
