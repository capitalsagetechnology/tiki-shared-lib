namespace Tiki.Shared.Auth;

/// <summary>
/// Issues and validates the inter-service token attached to every outbound gRPC/HTTP call
/// and checked on every inbound one. The interim implementation
/// (<see cref="HmacServiceTokenProvider"/>) is backed by a shared HMAC secret; swapping the
/// DI registration to Identity Service's real OAuth2 client-credentials issuance requires
/// no change outside <c>Program.cs</c> in any consuming service — every call site depends
/// only on this interface.
/// </summary>
public interface IServiceTokenProvider
{
    /// <summary>Returns a token identifying this service, for attaching to an outbound call.</summary>
    Task<string> GetTokenAsync(CancellationToken ct = default);

    /// <summary>Validates a token presented on an inbound call.</summary>
    Task<ServiceTokenValidationResult> ValidateAsync(string token, CancellationToken ct = default);
}

/// <summary>The outcome of validating an inbound service token.</summary>
public sealed record ServiceTokenValidationResult(bool IsValid, string? CallingService, string? FailureReason)
{
    public static ServiceTokenValidationResult Valid(string callingService) => new(true, callingService, null);
    public static ServiceTokenValidationResult Invalid(string reason) => new(false, null, reason);
}
