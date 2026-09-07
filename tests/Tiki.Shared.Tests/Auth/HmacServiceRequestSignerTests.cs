using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Tiki.Shared.Auth;
using Xunit;

namespace Tiki.Shared.Tests.Auth;

/// <summary>
/// End-to-end over the three checks that together make a signed request unforgeable and
/// unreplayable: signature, freshness, nonce. Each test removes exactly one of them.
/// </summary>
public class HmacServiceRequestSignerTests
{
    private const string GatewaySecret = "gateway-secret-at-least-32-bytes-long!!!";
    private const string WalletSecret = "wallet-secret-at-least-32-bytes-long!!!!";

    private static readonly SignableRequest Request =
        new("POST", "/api/tenants", null, HmacRequestSigner.EmptyBodyHash);

    private static (HmacServiceRequestSigner Gateway, HmacServiceRequestSigner Identity, FakeTimeProvider Clock) Mesh(
        bool requireNonce = true)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T12:00:00Z"));
        var nonces = new InMemoryNonceStore();

        // The gateway signs with its own secret; Identity verifies using its copy of the
        // gateway's secret. Per-service keys, not one mesh-wide password.
        var gateway = new HmacServiceRequestSigner(
            Options.Create(new ServiceIdentityOptions
            {
                ServiceId = "api-gateway",
                SigningSecret = GatewaySecret,
            }),
            nonces, clock, NullLogger<HmacServiceRequestSigner>.Instance);

        var identity = new HmacServiceRequestSigner(
            Options.Create(new ServiceIdentityOptions
            {
                ServiceId = "identity-api",
                SigningSecret = "identity-secret-at-least-32-bytes-long!!",
                TrustedCallers = new(StringComparer.OrdinalIgnoreCase) { ["api-gateway"] = GatewaySecret },
                RequireNonce = requireNonce,
            }),
            nonces, clock, NullLogger<HmacServiceRequestSigner>.Instance);

        return (gateway, identity, clock);
    }

    [Fact]
    public async Task A_request_signed_by_a_trusted_caller_is_accepted()
    {
        var (gateway, identity, _) = Mesh();

        var result = await identity.VerifyAsync(Request, gateway.Sign(Request));

        Assert.True(result.IsValid);
        Assert.Equal("api-gateway", result.CallingService);
    }

    [Fact]
    public async Task A_request_from_an_unknown_service_is_rejected()
    {
        var (_, identity, _) = Mesh();

        var rogue = new HmacServiceRequestSigner(
            Options.Create(new ServiceIdentityOptions
            {
                ServiceId = "not-a-real-service",
                SigningSecret = WalletSecret,
            }),
            new InMemoryNonceStore(), TimeProvider.System, NullLogger<HmacServiceRequestSigner>.Instance);

        var result = await identity.VerifyAsync(Request, rogue.Sign(Request));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task A_caller_impersonating_a_trusted_service_id_is_rejected()
    {
        // The attack the allow-list alone would not stop: claiming to be the gateway while
        // holding a different key. Only possession of the gateway's secret gets you through.
        var (_, identity, _) = Mesh();

        var impostor = new HmacServiceRequestSigner(
            Options.Create(new ServiceIdentityOptions
            {
                ServiceId = "api-gateway",
                SigningSecret = WalletSecret,
            }),
            new InMemoryNonceStore(), TimeProvider.System, NullLogger<HmacServiceRequestSigner>.Instance);

        var result = await identity.VerifyAsync(Request, impostor.Sign(Request));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task A_signature_captured_from_one_route_cannot_be_used_on_another()
    {
        var (gateway, identity, _) = Mesh();
        var signature = gateway.Sign(Request);

        var elsewhere = new SignableRequest(
            "DELETE", "/api/tenants/00000000-0000-0000-0000-000000000001", null, HmacRequestSigner.EmptyBodyHash);

        var result = await identity.VerifyAsync(elsewhere, signature);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task A_request_older_than_the_clock_skew_window_is_rejected()
    {
        var (gateway, identity, clock) = Mesh();
        var signature = gateway.Sign(Request);

        clock.Advance(TimeSpan.FromMinutes(6));   // default skew is 5 minutes

        var result = await identity.VerifyAsync(Request, signature);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task A_request_dated_too_far_in_the_future_is_rejected()
    {
        // Skew is a window in both directions. A one-sided check would let a caller with a
        // fast clock — or one deliberately post-dating — mint long-lived signatures.
        var (_, identity, clock) = Mesh();

        // A caller whose clock runs fast — modelled by giving the signer its own clock six
        // minutes ahead of the verifier's, rather than by rewinding the verifier's
        // (FakeTimeProvider refuses to go backwards).
        var fastGateway = new HmacServiceRequestSigner(
            Options.Create(new ServiceIdentityOptions
            {
                ServiceId = "api-gateway",
                SigningSecret = GatewaySecret,
            }),
            new InMemoryNonceStore(),
            new FakeTimeProvider(clock.GetUtcNow().AddMinutes(6)),
            NullLogger<HmacServiceRequestSigner>.Instance);

        var result = await identity.VerifyAsync(Request, fastGateway.Sign(Request));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Replaying_a_valid_request_is_rejected_the_second_time()
    {
        var (gateway, identity, _) = Mesh();
        var signature = gateway.Sign(Request);

        Assert.True((await identity.VerifyAsync(Request, signature)).IsValid);
        Assert.False((await identity.VerifyAsync(Request, signature)).IsValid);
    }

    [Fact]
    public async Task A_forged_request_does_not_consume_its_nonce()
    {
        // Nonce consumption happens only after the signature verifies. Otherwise an
        // unauthenticated attacker could burn the nonce a legitimate retry would reuse.
        var (gateway, identity, _) = Mesh();

        var genuine = gateway.Sign(Request);
        var forged = genuine with { Signature = "v1=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" };

        Assert.False((await identity.VerifyAsync(Request, forged)).IsValid);
        Assert.True((await identity.VerifyAsync(Request, genuine)).IsValid);
    }

    [Fact]
    public async Task Every_rejection_returns_the_same_message()
    {
        // A caller must not be able to tell which check failed — "bad signature" versus
        // "unknown service" versus "replay" is a map of what to try next.
        var (gateway, identity, clock) = Mesh();

        var unknown = await identity.VerifyAsync(Request, gateway.Sign(Request) with { ServiceId = "ghost" });
        var badSignature = await identity.VerifyAsync(Request, gateway.Sign(Request) with { Signature = "v1=bad" });

        var stale = gateway.Sign(Request);
        clock.Advance(TimeSpan.FromMinutes(6));
        var expired = await identity.VerifyAsync(Request, stale);

        Assert.Equal(unknown.FailureReason, badSignature.FailureReason);
        Assert.Equal(unknown.FailureReason, expired.FailureReason);
    }
}

/// <summary>
/// The nonce store's one job is an atomic test-and-set. These pin that, because a
/// read-then-write implementation passes every single-threaded test and still lets two
/// concurrent replays through.
/// </summary>
public class InMemoryNonceStoreTests
{
    [Fact]
    public async Task A_nonce_is_new_exactly_once()
    {
        var store = new InMemoryNonceStore();

        Assert.True(await store.TryConsumeAsync("api-gateway", "n1", TimeSpan.FromMinutes(10)));
        Assert.False(await store.TryConsumeAsync("api-gateway", "n1", TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public async Task The_same_nonce_from_two_services_does_not_collide()
    {
        var store = new InMemoryNonceStore();

        Assert.True(await store.TryConsumeAsync("api-gateway", "shared", TimeSpan.FromMinutes(10)));
        Assert.True(await store.TryConsumeAsync("wallet-service", "shared", TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public async Task Exactly_one_of_many_concurrent_consumers_wins()
    {
        // The race a GET-then-SET implementation loses. Every caller would observe "unseen"
        // before any of them wrote, and every replay would be accepted.
        var store = new InMemoryNonceStore();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 64).Select(_ =>
                Task.Run(() => store.TryConsumeAsync("api-gateway", "contended", TimeSpan.FromMinutes(10)))));

        Assert.Equal(1, results.Count(won => won));
    }
}
