using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tiki.Shared.Auth;
using Xunit;

namespace Tiki.Shared.Tests.Auth;

/// <summary>
/// The authorization default every service inherits from <see cref="TikiJwtExtensions"/>.
/// </summary>
/// <remarks>
/// ASP.NET Core treats an endpoint with no authorization metadata as anonymous. That default is
/// reasonable for a public web app and wrong for this platform, where the interesting endpoints
/// are all privileged: it means a controller shipped without <c>[Authorize]</c> is public, and
/// the omission looks exactly like every other file.
///
/// <para>
/// A fallback policy makes the omission fail loudly instead. This test guards it because the
/// symptom of losing it is invisible — nothing breaks, some endpoints just quietly stop needing
/// a token.
/// </para>
/// </remarks>
public class AuthorizationDefaultsTests
{
    private static AuthorizationOptions Options()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tiki:Auth:Jwt:Issuer"] = "https://identity.tiki.local",
                ["Tiki:Auth:Jwt:Audience"] = "tiki-platform",
                ["Tiki:Auth:Jwt:SigningKey"] = new string('k', 48),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTikiJwtAuth(configuration);

        return services.BuildServiceProvider()
            .GetRequiredService<IOptions<AuthorizationOptions>>().Value;
    }

    [Fact]
    public void An_endpoint_with_no_authorization_metadata_requires_authentication()
    {
        var fallback = Options().FallbackPolicy;

        // Without a fallback policy, a controller that forgets [Authorize] is public and
        // nothing in the code says so.
        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    /// <summary>
    /// The default policy — what a bare <c>[Authorize]</c> means — is left alone deliberately.
    /// Permissions are enforced by <c>[RequiresPermission]</c>, so folding a permission check
    /// into the default policy would make every authenticated endpoint demand something.
    /// </summary>
    [Fact]
    public void A_bare_Authorize_still_means_only_signed_in()
    {
        Assert.All(
            Options().DefaultPolicy.Requirements,
            r => Assert.IsType<DenyAnonymousAuthorizationRequirement>(r));
    }
}
