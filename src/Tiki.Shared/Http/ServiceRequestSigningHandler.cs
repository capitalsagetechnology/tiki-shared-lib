using System.Net.Http.Headers;
using System.Globalization;
using Tiki.Shared.Auth;
using Tiki.Shared.Gateway;

namespace Tiki.Shared.Http;

/// <summary>
/// Signs every outbound HTTP request on the <see cref="HttpClient"/> it is attached to, and
/// forwards the ambient correlation and identity context — so calling another Tiki service
/// is a plain <c>httpClient.GetAsync(...)</c> with no auth code at the call site, and
/// there is no way to forget the signature on one route.
/// </summary>
public sealed class ServiceRequestSigningHandler(IServiceRequestSigner signer) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri
            ?? throw new InvalidOperationException("Cannot sign a request with no URI. Set HttpClient.BaseAddress or pass an absolute URI.");

        // The body has to be hashed before it is sent, which means buffering it. These are
        // service-to-service JSON payloads, not uploads; anything large enough for this to
        // matter should be going through object storage rather than a signed API call.
        var bodyHash = request.Content is null
            ? HmacRequestSigner.EmptyBodyHash
            : HmacRequestSigner.HashBody(await request.Content.ReadAsByteArrayAsync(cancellationToken));

        var signature = signer.Sign(new SignableRequest(
            request.Method.Method, uri.AbsolutePath, uri.Query, bodyHash));

        request.Headers.TryAddWithoutValidation(TikiHeaderNames.ServiceId, signature.ServiceId);
        request.Headers.TryAddWithoutValidation(TikiHeaderNames.Timestamp, signature.Timestamp.ToString(CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation(TikiHeaderNames.Nonce, signature.Nonce);
        request.Headers.TryAddWithoutValidation(TikiHeaderNames.Signature, signature.Signature);

        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Copies the ambient correlation ids and forwarded identity onto every outbound request,
/// so a trace does not end at the first service boundary and the tenant filter still
/// applies two hops down.
/// </summary>
/// <remarks>
/// Ordered <em>before</em> <see cref="ServiceRequestSigningHandler"/> in the handler chain,
/// so the headers it adds are present when the signature is computed. Reversed, the
/// identity headers would travel unsigned — which is to say, forgeable.
/// </remarks>
public sealed class TikiContextPropagationHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Add(request.Headers, TikiHeaderNames.CorrelationId, ServiceContext.TraceId);
        Add(request.Headers, TikiHeaderNames.SessionId, ServiceContext.SessionId?.ToString());
        Add(request.Headers, TikiHeaderNames.TenantId, ServiceContext.TenantId?.ToString());
        Add(request.Headers, TikiHeaderNames.UserId, ServiceContext.UserId?.ToString());

        return base.SendAsync(request, cancellationToken);
    }

    private static void Add(HttpRequestHeaders headers, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !headers.Contains(name))
            headers.TryAddWithoutValidation(name, value);
    }
}
