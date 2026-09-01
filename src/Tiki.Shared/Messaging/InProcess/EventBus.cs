using Microsoft.Extensions.DependencyInjection;
using Tiki.Shared.Core.Events;

namespace Tiki.Shared.Messaging.InProcess;

/// <inheritdoc cref="IEventBus"/>
public sealed class EventBus(IServiceProvider serviceProvider) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : BaseEvent
    {
        var handlers = serviceProvider.GetServices<IEventHandler<TEvent>>()
            .OrderBy(handler => handler.GetType()
                .GetCustomAttributes(typeof(EventHandlerOrderAttribute), inherit: true)
                .Cast<EventHandlerOrderAttribute>()
                .Select(attribute => attribute.Order)
                .DefaultIfEmpty(int.MaxValue)
                .First())
            .ToList();

        List<Exception>? exceptions = null;

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(@event, ct);
            }
            catch (Exception ex)
            {
                (exceptions ??= []).Add(ex);
            }
        }

        if (exceptions is { Count: > 0 })
            throw new AggregateException($"One or more handlers failed for {typeof(TEvent).Name}.", exceptions);
    }
}
