using System.Text.Json.Serialization;

namespace Tiki.Shared.Auth.ApiKeys;

/// <summary>Which of a business's two environments a key belongs to.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ApiKeyEnvironment>))]
public enum ApiKeyEnvironment
{
    Sandbox,
    Live,
}

/// <summary>
/// Where a key was issued. A <see cref="LegacyChimoney"/> key was imported from the platform the
/// business API replaces, so the gateway may still route its calls to that platform for any
/// operation not yet rebuilt here — a key Tiki issued has no account there and never is.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ApiKeyOrigin>))]
public enum ApiKeyOrigin
{
    Tiki,
    LegacyChimoney,
}

/// <summary>
/// Everything the gateway needs to decide an <c>X-API-KEY</c> request, stored in Redis under the
/// key's hash. Identity writes it whenever a key, its environment or its business changes;
/// the gateway only reads.
/// </summary>
/// <remarks>
/// This is the API-key counterpart of <see cref="Sessions.TikiSession"/>, and for the same
/// reason: the gateway must not call Identity on every request. Holding the business-level
/// facts here (name, whether API access is on) means a suspension reaches the edge the
/// moment Identity re-projects the business, without a second lookup per call.
/// </remarks>
public sealed record ApiKeyGrant
{
    public required Guid KeyId { get; init; }
    public required Guid BusinessId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid EnvironmentId { get; init; }
    public required ApiKeyEnvironment Environment { get; init; }
    public ApiKeyOrigin Origin { get; init; } = ApiKeyOrigin.Tiki;

    /// <summary>Shown in the "API Access not enabled for account &lt;name&gt;" refusal.</summary>
    public required string BusinessName { get; init; }

    /// <summary>
    /// False when the business may not use the API right now — suspended, or a live key whose
    /// business has lost KYB approval. The key still resolves, so the caller gets the specific
    /// refusal instead of "key not valid", which would send them off to generate a new one.
    /// </summary>
    public bool ApiAccessEnabled { get; init; } = true;

    /// <summary>Machine-readable reason when <see cref="ApiAccessEnabled"/> is false, for logs.</summary>
    public string? DisabledReason { get; init; }

    /// <summary>
    /// IP addresses or CIDR ranges allowed to use the key. Empty means any address.
    /// </summary>
    public IReadOnlyList<string> AllowedIpRanges { get; init; } = [];

    /// <summary>
    /// Set on a key that has been rolled: it keeps working until this moment so the business
    /// can deploy the replacement. Null for a key with no end date.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
