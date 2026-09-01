namespace Tiki.Shared.Messaging.InProcess;

/// <summary>Controls dispatch order among multiple <see cref="IEventHandler{TEvent}"/> implementations for the same event — lower runs first. Handlers without this attribute run last, in registration order.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class EventHandlerOrderAttribute(int order) : Attribute
{
    public int Order { get; } = order;
}
