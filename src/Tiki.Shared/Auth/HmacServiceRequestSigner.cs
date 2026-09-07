using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tiki.Shared.Auth;

/// <summary>
/// The shared-secret HMAC implementation of both halves of service-to-service auth.
/// Signing uses this service's own <see cref="ServiceIdentityOptions.SigningSecret"/>;
/// verification looks the caller's secret up in
/// <see cref="ServiceIdentityOptions.TrustedCallers"/>.
/// </summary>
public sealed class HmacServiceRequestSigner(
    IOptions<ServiceIdentityOptions> options,
    INonceStore nonceStore,
    TimeProvider timeProvider,
    ILogger<HmacServiceRequestSigner> logger) : IServiceRequestSigner, IServiceRequestVerifier
{
    private readonly ServiceIdentityOptions _options = options.Value;

    public string ServiceId => _options.ServiceId;

    public RequestSignature Sign(SignableRequest request)
    {
        var timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var nonce = HmacRequestSigner.NewNonce();

        var canonical = HmacRequestSigner.BuildCanonicalRequest(
            _options.ServiceId, request.Method, request.Path, request.QueryString, timestamp, nonce, request.BodySha256);

        return new RequestSignature(
            _options.ServiceId, timestamp, nonce, HmacRequestSigner.Sign(canonical, _options.SigningSecret));
    }

    public async Task<ServiceAuthResult> VerifyAsync(
        SignableRequest request, RequestSignature signature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(signature.ServiceId) ||
            !_options.TrustedCallers.TryGetValue(signature.ServiceId, out var callerSecret))
        {
            logger.LogWarning("Rejected request from unknown or untrusted service {CallingService}.", signature.ServiceId);
            return Rejected();
        }

        // Freshness first: it is the cheapest check, and it is what bounds the replay
        // window for everything downstream of it.
        var age = timeProvider.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(signature.Timestamp);
        if (age.Duration() > _options.ClockSkew)
        {
            logger.LogWarning(
                "Rejected request from {CallingService}: timestamp {Age} outside the {Skew} skew window.",
                signature.ServiceId, age, _options.ClockSkew);
            return Rejected();
        }

        var canonical = HmacRequestSigner.BuildCanonicalRequest(
            signature.ServiceId, request.Method, request.Path, request.QueryString,
            signature.Timestamp, signature.Nonce, request.BodySha256);

        if (!HmacRequestSigner.Verify(canonical, callerSecret, signature.Signature))
        {
            logger.LogWarning(
                "Rejected request from {CallingService}: signature does not match {Method} {Path}.",
                signature.ServiceId, request.Method, request.Path);
            return Rejected();
        }

        // Nonce last, and only after the signature verifies. Consuming a nonce for a
        // request that turns out to be forged would let an unauthenticated caller burn
        // nonces, and — worse — a legitimate retry that happened to reuse one would then
        // be rejected. Only authentic requests get to claim one.
        if (_options.RequireNonce)
        {
            var retention = _options.ClockSkew * 2;
            if (!await nonceStore.TryConsumeAsync(signature.ServiceId, signature.Nonce, retention, ct))
            {
                logger.LogWarning(
                    "Rejected request from {CallingService}: nonce already used (replay).", signature.ServiceId);
                return Rejected();
            }
        }

        return ServiceAuthResult.Valid(signature.ServiceId);
    }

    /// <summary>One message for every rejection — the specific cause is in the log, not on the wire.</summary>
    private static ServiceAuthResult Rejected() =>
        ServiceAuthResult.Invalid("Service request authentication failed.");
}
