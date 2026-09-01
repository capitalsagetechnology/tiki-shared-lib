using Grpc.Core;
using Moq;
using Tiki.Shared.Auth;
using Tiki.Shared.Grpc;
using Xunit;

namespace Tiki.Shared.Tests.Grpc;

public class InterceptorTests
{
    [Fact]
    public async Task ServerInterceptor_rejects_a_call_with_no_token_header()
    {
        var tokenProvider = new Mock<IServiceTokenProvider>(MockBehavior.Strict);

        var result = await ServiceTokenAuthInterceptor.ValidateTokenAsync(new Metadata(), tokenProvider.Object, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("Missing service token.", result.FailureReason);
    }

    [Fact]
    public async Task ServerInterceptor_rejects_a_call_with_an_invalid_token()
    {
        var tokenProvider = new Mock<IServiceTokenProvider>();
        tokenProvider
            .Setup(p => p.ValidateAsync("bad-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceTokenValidationResult.Invalid("Signature mismatch."));

        var headers = new Metadata { { ServiceTokenClientInterceptor.TokenMetadataKey, "bad-token" } };

        var result = await ServiceTokenAuthInterceptor.ValidateTokenAsync(headers, tokenProvider.Object, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ServerInterceptor_accepts_a_call_with_a_valid_token()
    {
        var tokenProvider = new Mock<IServiceTokenProvider>();
        tokenProvider
            .Setup(p => p.ValidateAsync("good-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceTokenValidationResult.Valid("wallet-service"));

        var headers = new Metadata { { ServiceTokenClientInterceptor.TokenMetadataKey, "good-token" } };

        var result = await ServiceTokenAuthInterceptor.ValidateTokenAsync(headers, tokenProvider.Object, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("wallet-service", result.CallingService);
    }

    [Fact]
    public void ClientInterceptor_attaches_the_token_header()
    {
        var headers = ServiceTokenClientInterceptor.BuildHeadersWithToken(existingHeaders: null, token: "my-token");

        Assert.Equal("my-token", headers.GetValue(ServiceTokenClientInterceptor.TokenMetadataKey));
    }

    [Fact]
    public void ClientInterceptor_preserves_existing_headers()
    {
        var existing = new Metadata { { "x-custom", "value" } };

        var headers = ServiceTokenClientInterceptor.BuildHeadersWithToken(existing, "my-token");

        Assert.Equal("value", headers.GetValue("x-custom"));
        Assert.Equal("my-token", headers.GetValue(ServiceTokenClientInterceptor.TokenMetadataKey));
    }
}
