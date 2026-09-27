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

/// <summary>
/// A business payment moved to a new status. Published by Wallet on
/// <see cref="BusinessTopics.BusinessPaymentEvents"/> for every payment, however it was started,
/// so the business API can turn it into the integrator's webhook and the console can show it live.
/// </summary>
/// <remarks>
/// Carries the payment's state in full rather than a pointer to it: a webhook sent minutes later
/// must describe the payment as it was at this change, not as it is when the consumer gets round
/// to asking.
/// </remarks>
public sealed record BusinessPaymentStatusChangedEvent : BusinessEvent
{
    public override string EventType => BusinessEventTypes.PaymentStatusChanged;

    public required Guid PaymentId { get; init; }

    /// <summary>The caller's reference (for API payments, the business API's client reference).</summary>
    public required string Reference { get; init; }

    /// <summary><c>Sandbox</c> or <c>Live</c>.</summary>
    public required string Environment { get; init; }

    /// <summary>A BusinessPaymentStatus name, before and after.</summary>
    public string? PreviousStatus { get; init; }

    public required string Status { get; init; }

    /// <summary>"BankTransfer", "MobileMoney", "Interac" or "Spei".</summary>
    public required string Rail { get; init; }

    public required string CountryCode { get; init; }

    public required decimal PayoutAmount { get; init; }

    public required string PayoutCurrency { get; init; }

    public required decimal FundingAmount { get; init; }

    public required string FundingCurrency { get; init; }

    public decimal FeeAmount { get; init; }

    public string? ProviderReference { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureReason { get; init; }

    /// <summary>Set when an API key started the payment; null for a console payment.</summary>
    public Guid? ApiKeyId { get; init; }

    /// <summary>
    /// The business's customer the payment was made on behalf of (its originator), for monitoring
    /// and the integrator's records; null when the business paid for itself. Name and country are
    /// as they were when the payment was made.
    /// </summary>
    public Guid? CustomerId { get; init; }

    public string? CustomerName { get; init; }

    public string? CustomerCountry { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}

/// <summary>
/// A business pay-in moved to a new status. Published by Wallet on
/// <see cref="BusinessTopics.BusinessPaymentEvents"/> alongside payment status changes, keyed by
/// business id, so the business API can send <c>chimoney.payment.completed</c>.
/// </summary>
public sealed record BusinessPayInStatusChangedEvent : BusinessEvent
{
    public override string EventType => BusinessEventTypes.PayInStatusChanged;

    public required Guid PayInId { get; init; }

    /// <summary>The caller's client reference.</summary>
    public required string Reference { get; init; }

    /// <summary><c>Sandbox</c> or <c>Live</c>.</summary>
    public required string Environment { get; init; }

    /// <summary><c>InteracRequest</c> or <c>CardLink</c>.</summary>
    public required string Method { get; init; }

    public string? PreviousStatus { get; init; }

    /// <summary>Pending, Paid, Failed, Expired or Cancelled.</summary>
    public required string Status { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public decimal? CreditedAmount { get; init; }

    public string? CreditedCurrency { get; init; }

    public decimal FeeAmount { get; init; }

    public string? PayerEmail { get; init; }

    public string? PayerName { get; init; }

    public Guid? SubAccountId { get; init; }

    public string? ProviderReference { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureReason { get; init; }

    public Guid? ApiKeyId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}

/// <summary>The business event types published on <see cref="BusinessTopics.BusinessEvents"/>.</summary>
public static class BusinessEventTypes
{
    public const string Signup = "business.signup";
    public const string EnvironmentActivated = "business.environmentActivated";
    public const string PaymentStatusChanged = "business.payment.statusChanged";
    public const string PayInStatusChanged = "business.payin.statusChanged";
}
