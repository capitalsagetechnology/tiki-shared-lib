using System.Globalization;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Tiki.Shared.Auth;
using Tiki.Shared.Gateway;

namespace Tiki.Shared.Grpc;

/// <summary>
/// Signs every outbound gRPC call and forwards the ambient identity context — the gRPC
/// mirror of <see cref="Http.ServiceRequestSigningHandler"/>.
/// </summary>
/// <remarks>
/// gRPC is HTTP/2 POST underneath, and the method's full name (<c>/package.Service/Method</c>)
/// is its path — so the same canonical form covers both transports and one verifier
/// implementation serves them both.
///
/// <para>
/// The body is <em>not</em> hashed here. A gRPC request message is not materialised as
/// bytes at interception time (and may be a stream that can only be read once), so the
/// signature covers method, path, timestamp and nonce but not the payload. That is a real
/// gap relative to the HTTP path and worth naming: it means an attacker who can modify
/// traffic in flight could alter a message body without breaking the MAC. It is acceptable
/// only because inter-service gRPC runs over TLS inside the mesh, where an active
/// man-in-the-middle is already game over. The nonce and timestamp still make a captured
/// call unreplayable, which is what this interceptor exists for.
/// </para>
/// </remarks>
public sealed class ServiceSigningClientInterceptor(IServiceRequestSigner signer) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation) =>
        continuation(request, Signed(context));

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation) =>
        continuation(request, Signed(context));

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation) =>
        continuation(Signed(context));

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation) =>
        continuation(request, Signed(context));

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation) =>
        continuation(Signed(context));

    private ClientInterceptorContext<TRequest, TResponse> Signed<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var headers = BuildHeaders(context.Options.Headers, signer, context.Method.FullName);
        return new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, context.Options.WithHeaders(headers));
    }

    /// <summary>Header assembly on its own, so it can be unit-tested without a live channel.</summary>
    internal static Metadata BuildHeaders(Metadata? existing, IServiceRequestSigner signer, string methodFullName)
    {
        var headers = existing ?? [];

        var signature = signer.Sign(new SignableRequest(
            "POST", methodFullName, QueryString: null, HmacRequestSigner.EmptyBodyHash));

        headers.Add(TikiHeaderNames.ServiceId, signature.ServiceId);
        headers.Add(TikiHeaderNames.Timestamp, signature.Timestamp.ToString(CultureInfo.InvariantCulture));
        headers.Add(TikiHeaderNames.Nonce, signature.Nonce);
        headers.Add(TikiHeaderNames.Signature, signature.Signature);

        // Same ordering rule as the HTTP handler: these go on before the signature is
        // computed... except gRPC metadata is unordered, so instead they are simply included
        // in the canonical form's path/method only. Identity headers here are advisory and
        // the receiver re-derives tenant from the session, never from these.
        AddIfPresent(headers, TikiHeaderNames.CorrelationId, ServiceContext.TraceId);
        AddIfPresent(headers, TikiHeaderNames.TenantId, ServiceContext.TenantId?.ToString());
        AddIfPresent(headers, TikiHeaderNames.UserId, ServiceContext.UserId?.ToString());

        return headers;
    }

    private static void AddIfPresent(Metadata headers, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            headers.Add(key, value);
    }
}
