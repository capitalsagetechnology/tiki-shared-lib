using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Tiki.Shared.Auth.ApiKeys;

/// <summary>
/// Turns a presented API key into the value it is stored and looked up by:
/// lower-case hex HMAC-SHA256 of the key under <see cref="ApiKeyOptions.Pepper"/>.
/// </summary>
/// <remarks>
/// Format-agnostic on purpose. Keys Tiki mints look like <c>sk_live_…</c>, keys imported from
/// the legacy platform do not, and both must resolve — so nothing here inspects a prefix.
/// </remarks>
public sealed class ApiKeyHasher(IOptions<ApiKeyOptions> options)
{
    private readonly byte[] _pepper = Encoding.UTF8.GetBytes(options.Value.Pepper);

    public string Hash(string apiKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKey);
        return Convert.ToHexStringLower(HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(apiKey)));
    }
}
