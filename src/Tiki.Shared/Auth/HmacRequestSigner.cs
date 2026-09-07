using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Tiki.Shared.Auth;

/// <summary>
/// Signs and verifies a service-to-service call by computing an HMAC-SHA256 over a
/// <em>canonical representation of the request itself</em> — method, path, query,
/// timestamp, nonce and a hash of the body — rather than over nothing at all.
///
/// <para>
/// This replaces the earlier bearer-style token, which was a signed
/// <c>serviceId.expiry</c> string and nothing more. That token was a password: anyone who
/// observed one could replay it against <em>any</em> endpoint, with <em>any</em> body,
/// until it expired. A signature that covers the request binds the credential to the one
/// call it was minted for — a captured signature cannot be pointed at a different route or
/// have its payload edited, because either change breaks the MAC.
/// </para>
///
/// <para>
/// Three things must all hold for a request to be accepted, and each closes a different
/// hole: the signature must verify (authenticity + integrity), the timestamp must be
/// inside <see cref="ServiceIdentityOptions.ClockSkew"/> (bounds the replay window), and
/// the nonce must not have been seen before (closes the window entirely — see
/// <see cref="INonceStore"/>).
/// </para>
/// </summary>
public static class HmacRequestSigner
{
    /// <summary>Scheme version, carried in the signature header so v2 can ship without a flag day.</summary>
    public const string SchemeVersion = "v1";

    /// <summary>SHA-256 of the empty body, precomputed — the common case for GET and DELETE.</summary>
    public static readonly string EmptyBodyHash = ToHex(SHA256.HashData([]));

    /// <summary>
    /// Builds the exact string both sides MAC. Signer and verifier must produce this
    /// byte-for-byte, so every element is normalised here and nowhere else:
    /// method upper-cased, path as-is (it is already the routing key), query parameters
    /// sorted by name so ordering cannot change the signature, and the body reduced to a
    /// hex SHA-256 so a large payload does not have to be buffered twice.
    /// </summary>
    public static string BuildCanonicalRequest(
        string serviceId, string method, string path, string? queryString, long timestamp, string nonce, string bodySha256)
    {
        var canonicalQuery = CanonicaliseQuery(queryString);

        return string.Join('\n',
            SchemeVersion,
            serviceId,
            method.ToUpperInvariant(),
            path,
            canonicalQuery,
            timestamp.ToString(CultureInfo.InvariantCulture),
            nonce,
            bodySha256);
    }

    /// <summary>Computes the signature header value, <c>v1=&lt;base64url&gt;</c>.</summary>
    public static string Sign(string canonicalRequest, string secret)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(canonicalRequest));
        return $"{SchemeVersion}={Base64Url.EncodeToString(mac)}";
    }

    /// <summary>
    /// Verifies a presented signature against a freshly computed one in fixed time.
    /// <see cref="CryptographicOperations.FixedTimeEquals"/> is the point: a naive
    /// comparison returns faster the earlier it finds a mismatched byte, which leaks the
    /// correct signature one byte at a time to a caller willing to measure.
    /// </summary>
    public static bool Verify(string canonicalRequest, string secret, string presentedSignature)
    {
        var expected = Sign(canonicalRequest, secret);

        // Compare the raw bytes of both, including the version prefix — a caller
        // presenting "v2=..." must not be accepted by a v1 verifier.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(presentedSignature));
    }

    /// <summary>Lower-case hex SHA-256 of a request body. An empty body yields <see cref="EmptyBodyHash"/>.</summary>
    public static string HashBody(ReadOnlySpan<byte> body) =>
        body.IsEmpty ? EmptyBodyHash : ToHex(SHA256.HashData(body));

    /// <summary>Streaming overload, for a request body that has not been buffered.</summary>
    public static async Task<string> HashBodyAsync(Stream body, CancellationToken ct = default)
    {
        var hash = await SHA256.HashDataAsync(body, ct);
        return ToHex(hash);
    }

    /// <summary>A fresh 128-bit nonce. Never reuse one — the verifier rejects a repeat.</summary>
    public static string NewNonce() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    /// <summary>
    /// Sorted <c>name=value</c> pairs joined by <c>&amp;</c>. Sorting matters: the same
    /// logical request can arrive with query parameters in any order (proxies and client
    /// libraries both reorder them), and an order-sensitive canonical form would reject
    /// perfectly valid traffic.
    /// </summary>
    private static string CanonicaliseQuery(string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
            return string.Empty;

        var pairs = queryString.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Order(StringComparer.Ordinal);

        return string.Join('&', pairs);
    }

    private static string ToHex(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(bytes);
}
