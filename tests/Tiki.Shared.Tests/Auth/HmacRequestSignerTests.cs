using Tiki.Shared.Auth;
using Xunit;

namespace Tiki.Shared.Tests.Auth;

/// <summary>
/// The canonical form is the whole security property: signer and verifier must agree on it
/// byte-for-byte, and every element of a request that matters must be inside it. These tests
/// pin both halves — a change that drops a field from the canonical string, or that
/// normalises one side but not the other, fails here rather than in production.
/// </summary>
public class HmacRequestSignerTests
{
    private const string Secret = "a-secret-that-is-at-least-32-bytes-long!!";

    private static string Canonical(
        string method = "POST",
        string path = "/api/tenants",
        string? query = null,
        long timestamp = 1_700_000_000,
        string nonce = "nonce-1",
        string? bodyHash = null) =>
        HmacRequestSigner.BuildCanonicalRequest(
            "identity-api", method, path, query, timestamp, nonce, bodyHash ?? HmacRequestSigner.EmptyBodyHash);

    [Fact]
    public void A_signature_verifies_against_the_same_canonical_request()
    {
        var canonical = Canonical();
        var signature = HmacRequestSigner.Sign(canonical, Secret);

        Assert.True(HmacRequestSigner.Verify(canonical, Secret, signature));
    }

    [Fact]
    public void A_signature_does_not_verify_under_a_different_secret()
    {
        var canonical = Canonical();
        var signature = HmacRequestSigner.Sign(canonical, Secret);

        Assert.False(HmacRequestSigner.Verify(canonical, "a-different-secret-of-sufficient-length!!", signature));
    }

    [Theory]
    // Each case changes exactly one element of the request. All of them must break the
    // signature — that is what "the credential is bound to this one call" means. If any
    // stops failing, a captured signature has become reusable for that variation.
    [InlineData("GET", "/api/tenants", null, 1_700_000_000, "nonce-1")]
    [InlineData("POST", "/api/tenants/other", null, 1_700_000_000, "nonce-1")]
    [InlineData("POST", "/api/tenants", "page=2", 1_700_000_000, "nonce-1")]
    [InlineData("POST", "/api/tenants", null, 1_700_000_001, "nonce-1")]
    [InlineData("POST", "/api/tenants", null, 1_700_000_000, "nonce-2")]
    public void Changing_any_signed_element_breaks_the_signature(
        string method, string path, string? query, long timestamp, string nonce)
    {
        var signature = HmacRequestSigner.Sign(Canonical(), Secret);
        var tampered = Canonical(method, path, query, timestamp, nonce);

        Assert.False(HmacRequestSigner.Verify(tampered, Secret, signature));
    }

    [Fact]
    public void Changing_the_body_breaks_the_signature()
    {
        var original = Canonical(bodyHash: HmacRequestSigner.HashBody("{\"amount\":10}"u8));
        var signature = HmacRequestSigner.Sign(original, Secret);

        var tampered = Canonical(bodyHash: HmacRequestSigner.HashBody("{\"amount\":1000000}"u8));

        Assert.False(HmacRequestSigner.Verify(tampered, Secret, signature));
    }

    [Fact]
    public void Query_parameter_order_does_not_change_the_signature()
    {
        // Proxies and client libraries reorder query strings freely. If ordering mattered,
        // a perfectly valid request would be rejected depending on which hop it took.
        var a = Canonical(query: "?page=2&size=50&sort=name");
        var b = Canonical(query: "?sort=name&page=2&size=50");

        Assert.Equal(a, b);
    }

    [Fact]
    public void An_absent_query_string_and_an_empty_one_are_the_same_request()
    {
        Assert.Equal(Canonical(query: null), Canonical(query: ""));
        Assert.Equal(Canonical(query: null), Canonical(query: "?"));
    }

    [Fact]
    public void A_signature_from_a_future_scheme_version_is_rejected()
    {
        // Verify compares the version prefix too, so a v1 verifier cannot be talked into
        // accepting a value that claims to be something else.
        var canonical = Canonical();
        var v1 = HmacRequestSigner.Sign(canonical, Secret);
        var forged = "v2=" + v1.Split('=', 2)[1];

        Assert.False(HmacRequestSigner.Verify(canonical, Secret, forged));
    }

    [Fact]
    public void Nonces_do_not_repeat()
    {
        var nonces = Enumerable.Range(0, 1_000).Select(_ => HmacRequestSigner.NewNonce()).ToList();

        Assert.Equal(nonces.Count, nonces.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void An_empty_body_hashes_to_the_documented_constant()
    {
        Assert.Equal(HmacRequestSigner.EmptyBodyHash, HmacRequestSigner.HashBody([]));
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            HmacRequestSigner.EmptyBodyHash);
    }
}
