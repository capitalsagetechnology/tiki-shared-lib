namespace Tiki.Shared.Auth;

/// <summary>A request about to leave this service, reduced to the parts that get signed.</summary>
/// <param name="Method">HTTP verb, or <c>POST</c> for gRPC (which is always POST on the wire).</param>
/// <param name="Path">Absolute path — for gRPC, <c>/package.Service/Method</c>.</param>
/// <param name="QueryString">Raw query string, with or without the leading <c>?</c>. Null when there is none.</param>
/// <param name="BodySha256">Lower-case hex SHA-256 of the body, or <see cref="HmacRequestSigner.EmptyBodyHash"/>.</param>
public readonly record struct SignableRequest(string Method, string Path, string? QueryString, string BodySha256);

/// <summary>The headers to attach to an outbound request so the receiver can verify it.</summary>
public readonly record struct RequestSignature(string ServiceId, long Timestamp, string Nonce, string Signature);

/// <summary>
/// Signs outbound service-to-service requests. Every call site depends on this interface
/// and never on the algorithm, so moving from the shared-secret HMAC scheme to
/// asymmetric signing (or to mTLS plus a bare identity header) is a DI registration change
/// in <c>Program.cs</c> and nothing else.
/// </summary>
public interface IServiceRequestSigner
{
    /// <summary>This service's own id, as it will appear to the receiver.</summary>
    string ServiceId { get; }

    /// <summary>Produces the signature headers for one outbound request.</summary>
    RequestSignature Sign(SignableRequest request);
}

/// <summary>Verifies inbound service-to-service requests. The mirror of <see cref="IServiceRequestSigner"/>.</summary>
public interface IServiceRequestVerifier
{
    /// <summary>
    /// Checks signature, freshness and nonce together. Returns the calling service's id on
    /// success; on failure the reason is deliberately coarse (see
    /// <see cref="ServiceAuthResult"/>) so a rejected caller learns nothing about which of
    /// the three checks failed.
    /// </summary>
    Task<ServiceAuthResult> VerifyAsync(SignableRequest request, RequestSignature signature, CancellationToken ct = default);
}

/// <summary>The outcome of verifying an inbound signed request.</summary>
public sealed record ServiceAuthResult(bool IsValid, string? CallingService, string? FailureReason)
{
    public static ServiceAuthResult Valid(string callingService) => new(true, callingService, null);

    /// <summary>
    /// One failure shape for every rejection. The detailed reason is logged server-side;
    /// what goes back on the wire stays generic, because "signature mismatch" versus
    /// "unknown service" versus "replayed nonce" tells an attacker which part of the
    /// forgery to work on next.
    /// </summary>
    public static ServiceAuthResult Invalid(string reason) => new(false, null, reason);
}
