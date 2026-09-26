using System.Text.Json;
using Microsoft.Extensions.Options;
using Tiki.Shared.Auth.ApiKeys;
using Tiki.Shared.Gateway;
using Xunit;

namespace Tiki.Shared.Tests.Auth;

/// <summary>
/// Identity writes key hashes and grants; the gateway reads them. Both sides compile against
/// these types, so what these tests pin is the agreement between them.
/// </summary>
public class ApiKeyTests
{
    private const string Pepper = "a-pepper-that-is-at-least-32-characters-long";

    private static ApiKeyHasher Hasher(string pepper = Pepper) =>
        new(Options.Create(new ApiKeyOptions { Pepper = pepper }));

    [Fact]
    public void The_same_key_always_hashes_to_the_same_value()
    {
        Assert.Equal(Hasher().Hash("sk_live_abc"), Hasher().Hash("sk_live_abc"));
    }

    [Fact]
    public void The_hash_is_lower_case_hex_of_a_256_bit_mac()
    {
        var hash = Hasher().Hash("sk_test_abc");

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void A_different_pepper_gives_a_different_hash()
    {
        Assert.NotEqual(Hasher().Hash("sk_live_abc"), Hasher(Pepper + "!").Hash("sk_live_abc"));
    }

    [Fact]
    public void Keys_without_the_sk_prefix_hash_like_any_other()
    {
        // Imported legacy keys carry no prefix and must still resolve.
        Assert.Matches("^[0-9a-f]{64}$", Hasher().Hash("0f6c2a1e-legacy-key"));
    }

    [Fact]
    public void An_empty_key_is_refused_rather_than_hashed()
    {
        Assert.ThrowsAny<ArgumentException>(() => Hasher().Hash(""));
    }

    [Fact]
    public void A_grant_round_trips_with_enums_as_names()
    {
        var grant = new ApiKeyGrant
        {
            KeyId = Guid.NewGuid(),
            BusinessId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EnvironmentId = Guid.NewGuid(),
            Environment = ApiKeyEnvironment.Live,
            Origin = ApiKeyOrigin.LegacyChimoney,
            BusinessName = "Acme Ltd",
            ApiAccessEnabled = false,
            DisabledReason = "business_suspended",
            AllowedIpRanges = ["203.0.113.0/24"],
            ExpiresAt = DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
        };

        var json = JsonSerializer.Serialize(grant, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"environment\":\"Live\"", json);
        Assert.Contains("\"origin\":\"LegacyChimoney\"", json);

        var back = JsonSerializer.Deserialize<ApiKeyGrant>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(grant with { AllowedIpRanges = back.AllowedIpRanges }, back);
        Assert.Equal(grant.AllowedIpRanges, back.AllowedIpRanges);
    }

    [Theory]
    [InlineData(TikiHeaderNames.BusinessId)]
    [InlineData(TikiHeaderNames.ApiKeyId)]
    [InlineData(TikiHeaderNames.ApiEnvironment)]
    public void Business_api_identity_headers_are_stripped_from_clients(string header)
    {
        Assert.Contains(header, TikiHeaderNames.StrippedFromClient);
    }

    [Fact]
    public void The_api_key_itself_is_not_stripped()
    {
        // The gateway must read it, and forwards it to the legacy platform for imported keys.
        Assert.DoesNotContain(TikiHeaderNames.ApiKey, TikiHeaderNames.StrippedFromClient);
    }
}
