using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Tiki.Shared.Auth;

/// <summary>Options for <see cref="HmacServiceTokenProvider"/>, bound from <c>IConfiguration</c>.</summary>
public sealed class HmacServiceTokenOptions
{
    public const string SectionName = "Tiki:Auth:HmacServiceToken";

    /// <summary>This service's own id, e.g. <c>"wallet-service"</c> — embedded in every token it issues.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Secret shared by every service in the mesh for v1. Never logged, never in an OTel span.</summary>
    public required string SharedSecret { get; init; }

    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Interim <see cref="IServiceTokenProvider"/> backed by a shared HMAC secret — a stand-in
/// until Identity Service ships real OAuth2 client-credentials issuance (see
/// <see cref="JwksClient"/>, ready for that swap). The token is
/// <c>base64url(serviceId.expiryUnixSeconds).base64url(hmacSha256)</c>; validation uses a
/// fixed-time comparison to avoid a timing side-channel.
/// </summary>
public sealed class HmacServiceTokenProvider(IOptions<HmacServiceTokenOptions> options) : IServiceTokenProvider
{
    private readonly HmacServiceTokenOptions _options = options.Value;

    public Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        var expiry = DateTimeOffset.UtcNow.Add(_options.TokenLifetime).ToUnixTimeSeconds();
        var payload = $"{_options.ServiceId}.{expiry}";
        var signature = Sign(payload, _options.SharedSecret);

        return Task.FromResult($"{Base64UrlEncode(Encoding.UTF8.GetBytes(payload))}.{Base64UrlEncode(signature)}");
    }

    public Task<ServiceTokenValidationResult> ValidateAsync(string token, CancellationToken ct = default)
    {
        var parts = token.Split('.', 2);
        if (parts.Length != 2)
            return Task.FromResult(ServiceTokenValidationResult.Invalid("Malformed service token."));

        string payload;
        byte[] signature;
        try
        {
            payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            signature = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return Task.FromResult(ServiceTokenValidationResult.Invalid("Malformed service token."));
        }

        var expectedSignature = Sign(payload, _options.SharedSecret);
        if (!CryptographicOperations.FixedTimeEquals(signature, expectedSignature))
            return Task.FromResult(ServiceTokenValidationResult.Invalid("Signature mismatch."));

        var payloadParts = payload.Split('.', 2);
        if (payloadParts.Length != 2 || !long.TryParse(payloadParts[1], out var expiryUnix))
            return Task.FromResult(ServiceTokenValidationResult.Invalid("Malformed service token payload."));

        if (DateTimeOffset.FromUnixTimeSeconds(expiryUnix) < DateTimeOffset.UtcNow)
            return Task.FromResult(ServiceTokenValidationResult.Invalid("Service token expired."));

        return Task.FromResult(ServiceTokenValidationResult.Valid(payloadParts[0]));
    }

    private static byte[] Sign(string payload, string secret) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
