using Tiki.Shared.Core.Events;

namespace Tiki.Shared.Messaging.InProcess;

/// <summary>Handles an event dispatched in-process by <see cref="IEventBus"/>.</summary>
public interface IEventHandler<in TEvent> where TEvent : BaseEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
