using Grpc.Core;
using Grpc.Core.Interceptors;
using Tiki.Shared.Auth;

namespace Tiki.Shared.Grpc;

/// <summary>
/// Attaches this service's token to every outbound gRPC call — zero per-call code in the
/// service. Trace context is left to the OpenTelemetry gRPC client instrumentation wired
/// by <c>AddTikiTelemetry</c>, not duplicated here.
/// </summary>
public sealed class ServiceTokenClientInterceptor(IServiceTokenProvider tokenProvider) : Interceptor
{
    public const string TokenMetadataKey = "x-service-token";

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation) =>
        continuation(request, WithToken(context));

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation) =>
        continuation(request, WithToken(context));

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation) =>
        continuation(WithToken(context));

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation) =>
        continuation(request, WithToken(context));

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation) =>
        continuation(WithToken(context));

    /// <remarks>
    /// Blocks on <see cref="IServiceTokenProvider.GetTokenAsync"/> — safe today because the
    /// interim <see cref="HmacServiceTokenProvider"/> completes synchronously. Revisit if a
    /// future provider (e.g. real OAuth2 client-credentials, which may need a network round
    /// trip to refresh) makes this a genuine async wait.
    /// </remarks>
    private ClientInterceptorContext<TRequest, TResponse> WithToken<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var token = tokenProvider.GetTokenAsync().GetAwaiter().GetResult();
        var options = context.Options.WithHeaders(BuildHeadersWithToken(context.Options.Headers, token));
        return new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);
    }

    /// <summary>Header-building logic on its own, independent of <see cref="ClientInterceptorContext{TRequest,TResponse}"/> — easy to unit test.</summary>
    internal static Metadata BuildHeadersWithToken(Metadata? existingHeaders, string token)
    {
        var headers = existingHeaders ?? new Metadata();
        headers.Add(TokenMetadataKey, token);
        return headers;
    }
}
