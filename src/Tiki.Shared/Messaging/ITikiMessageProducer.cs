using Tiki.Shared.Core.Events;

namespace Tiki.Shared.Messaging;

/// <summary>Publishes a domain event or directed message to Redpanda.</summary>
public interface ITikiMessageProducer
{
    /// <summary>
    /// <paramref name="partitionKey"/> is never optional — it must be the id of the entity
    /// that needs ordered delivery (e.g. the transaction id for a transaction-scoped
    /// event), so two messages about the same entity always land on the same partition.
    /// </summary>
    Task PublishAsync<T>(string topic, string partitionKey, T message, CancellationToken ct = default)
        where T : BaseEvent;
}
