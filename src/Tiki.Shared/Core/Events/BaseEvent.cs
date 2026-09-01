namespace Tiki.Shared.Core.Events;

/// <summary>
/// The only concrete type in <c>Core/Events</c> — envelope fields only. No PR may add a
/// concrete domain event type to this namespace: <c>wallet.balance-updated</c>,
/// <c>compliance.screen-transaction-requested</c>, and every other decision-shaped event
/// are defined in the owning service's own <c>*.Contracts</c> package and merely inherit
/// this envelope.
/// </summary>
public abstract record BaseEvent
{
    /// <summary>Unique id for this event instance — not the subject's id.</summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>UTC timestamp the event was raised, not when it is eventually handled.</summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>The distributed trace id this event was produced under, propagated end-to-end.</summary>
    public required string TraceId { get; init; }

    /// <summary>The service that published this event, e.g. <c>"wallet-service"</c>.</summary>
    public required string SourceService { get; init; }

    /// <summary>The id of the entity this event is about — the same id used as the Kafka partition key.</summary>
    public required string SubjectId { get; init; }
}
