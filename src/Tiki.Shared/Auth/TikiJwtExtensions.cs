using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tiki.Shared.Auth.Sessions;

namespace Tiki.Shared.Auth;

public static class TikiJwtExtensions
{
    /// <summary>
    /// Registers end-user JWT bearer authentication with Tiki's validation rules —
    /// one call, identical behaviour in every service.
    /// </summary>
    /// <remarks>
    /// Three settings here are not the framework defaults, and each one is a real hole
    /// closed rather than a preference:
    /// <list type="bullet">
    /// <item><c>MapInboundClaims = false</c> — .NET otherwise rewrites short claim names
    /// into SOAP-era URIs, so code reading <c>sub</c> silently finds nothing and treats
    /// the request as anonymous.</item>
    /// <item><c>ClockSkew</c> cut from 5 minutes to 30 seconds — the default keeps an
    /// expired token usable for five minutes past its own <c>exp</c>.</item>
    /// <item>A <c>token_type</c> check — a refresh token is a valid signed JWT from the
    /// same issuer, so without this it would be accepted as an access token, defeating the
    /// point of having short-lived access tokens at all.</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddTikiJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(TikiJwtOptions.SectionName).Get<TikiJwtOptions>()
            ?? throw new InvalidOperationException(
                $"Configuration section '{TikiJwtOptions.SectionName}' is missing. Every Tiki service must configure JWT validation explicitly.");

        if (string.IsNullOrWhiteSpace(options.SigningKey) == string.IsNullOrWhiteSpace(options.JwksUri))
        {
            throw new InvalidOperationException(
                $"Set exactly one of '{TikiJwtOptions.SectionName}:SigningKey' or '{TikiJwtOptions.SectionName}:JwksUri'.");
        }

        services.AddSingleton(options);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = options.ClockSkew,
                    NameClaimType = TikiClaimTypes.UserId,
                    RoleClaimType = TikiClaimTypes.Permission,
                };

                if (!string.IsNullOrWhiteSpace(options.SigningKey))
                {
                    jwt.TokenValidationParameters.IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));

                    // Pin the algorithm. Without this, a token whose header says "alg":"none"
                    // — or one signed with a weaker algorithm the library also accepts — is
                    // a candidate for validation.
                    jwt.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
                }
                else
                {
                    jwt.MetadataAddress = options.JwksUri!;
                    jwt.RequireHttpsMetadata = !options.JwksUri!.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase);
                    jwt.TokenValidationParameters.ValidAlgorithms =
                        [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.EcdsaSha256];
                }

                jwt.Events = new JwtBearerEvents { OnTokenValidated = OnTokenValidatedAsync };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<ISessionAccessor, HttpContextSessionAccessor>();
        services.AddSingleton<IAuthorizationHandler, Authorization.PermissionAuthorizationHandler>();
        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Runs once the token's signature, issuer, audience and lifetime have all checked out —
    /// and turns "this token was validly issued at some point" into "this user is logged in
    /// right now, and here is what they may do".
    ///
    /// <para>
    /// A signed JWT is, on its own, an unrevocable claim: once issued it stays valid until
    /// it expires, and nothing a user does — logging out, changing a password, having their
    /// access removed — can call it back. That is why the token carries only a session
    /// pointer (<c>sid</c>) and the authority lives in Redis: the session record is
    /// deletable, so logout takes effect immediately across every service instead of leaving
    /// a token usable for the rest of its lifetime.
    /// </para>
    /// </summary>
    private static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        var principal = context.Principal!;
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>().CreateLogger(typeof(TikiJwtExtensions));

        // A refresh token is a validly-signed JWT from the same issuer. Without this check
        // it would sail through every validation above and be accepted as an access token,
        // which would make short access-token lifetimes pointless.
        if (!string.Equals(principal.FindFirst(TikiClaimTypes.TokenType)?.Value, "access", StringComparison.Ordinal))
        {
            context.Fail("Only an access token may be presented as a bearer token.");
            return;
        }

        if (!Guid.TryParse(principal.FindFirst(TikiClaimTypes.SessionId)?.Value, out var sessionId))
        {
            context.Fail("Access token carries no session id.");
            return;
        }

        var store = context.HttpContext.RequestServices.GetRequiredService<ISessionStore>();
        var session = await store.GetAsync(sessionId, context.HttpContext.RequestAborted);

        if (session is null)
        {
            // The signature was fine; the session behind it is gone. Logged out, revoked,
            // or expired — from the caller's point of view all three are "log in again".
            logger.LogInformation("Rejected token for session {SessionId}: session is no longer live.", sessionId);
            context.Fail("Session is no longer valid.");
            return;
        }

        // The token and the session must agree about who this is. They can only disagree if
        // a token was minted against one session and presented alongside another's id, so
        // treat it as an attack rather than a mismatch to reconcile.
        if (Guid.TryParse(principal.FindFirst(TikiClaimTypes.UserId)?.Value, out var tokenUserId) &&
            tokenUserId != session.UserId)
        {
            logger.LogWarning(
                "Rejected token for session {SessionId}: token subject {TokenUserId} does not match session user {SessionUserId}.",
                sessionId, tokenUserId, session.UserId);
            context.Fail("Token does not match its session.");
            return;
        }

        context.HttpContext.Items[HttpContextSessionAccessor.ItemsKey] = session;

        // Ambient context comes from the *session*, not the token: the session is current,
        // and a long-lived token's copy of the tenant may be stale.
        ServiceContext.TenantId = session.TenantId;
        ServiceContext.UserId = session.UserId;
        ServiceContext.UserType = session.UserType;

        var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<SessionOptions>>().Value;
        if (options.SlidingExpiration)
            await store.TouchAsync(sessionId, options.Lifetime, context.HttpContext.RequestAborted);
    }
}
