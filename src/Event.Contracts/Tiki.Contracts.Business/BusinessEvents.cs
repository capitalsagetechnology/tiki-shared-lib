using Tiki.Shared.Core.Events;

namespace Tiki.Contracts.Business;

/// <summary>
/// Fields every business event carries, on top of the platform envelope. All business events
/// share one topic (<see cref="BusinessTopics.BusinessEvents"/>) with <see cref="EventType"/>
/// as the discriminator — see that constant's own remarks for why.
/// </summary>
public abstract record BusinessEvent : BaseEvent
{
    /// <summary>
    /// One of <see cref="BusinessEventTypes"/>. A computed override on each concrete event
    /// rather than a settable field, so a producer can never construct, say, a
    /// <see cref="BusinessSignedUpEvent"/> carrying the wrong event type by mistake.
    /// </summary>
    public abstract string EventType { get; }

    public required Guid BusinessId { get; init; }

    public required Guid TenantId { get; init; }
}

/// <summary>
/// A business completed signup: email verified, password set, access token issued. Raised once,
/// at that moment — not at the start of signup, when the account is still provisional and may
/// never be completed.
/// </summary>
public sealed record BusinessSignedUpEvent : BusinessEvent
{
    public override string EventType => BusinessEventTypes.Signup;

    /// <summary>
    /// The environment provisioned active by default (Sandbox), so a consumer — Wallet,
    /// Accounts — can provision matching resources for it without a follow-up query.
    /// </summary>
    public required Guid ActiveEnvironmentId { get; init; }

    /// <summary><c>Sandbox</c> or <c>Live</c>.</summary>
    public required string ActiveEnvironmentType { get; init; }
}

/// <summary>A business switched which environment (Sandbox/Live) is active.</summary>
public sealed record BusinessEnvironmentActivatedEvent : BusinessEvent
{
    public override string EventType => BusinessEventTypes.EnvironmentActivated;

    public required Guid EnvironmentId { get; init; }

    /// <summary><c>Sandbox</c> or <c>Live</c>.</summary>
    public required string EnvironmentType { get; init; }
}

/// <summary>The business event types published on <see cref="BusinessTopics.BusinessEvents"/>.</summary>
public static class BusinessEventTypes
{
    public const string Signup = "business.signup";
    public const string EnvironmentActivated = "business.environmentActivated";
}
