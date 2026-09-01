using Tiki.Shared.Core.Events;

namespace Tiki.Shared.Messaging.InProcess;

/// <summary>
/// In-process event dispatch — publishes a <see cref="BaseEvent"/> to every registered
/// <see cref="IEventHandler{TEvent}"/> within the same service, in-memory, no broker
/// involved. For anything that crosses a service boundary, use <see cref="ITikiMessageProducer"/> instead.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : BaseEvent;
}
