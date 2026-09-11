namespace Tiki.Contracts.Business;

/// <summary>
/// The topic Identity publishes every business event to.
/// </summary>
/// <remarks>
/// One topic for every business event kind, not one per kind the way
/// <c>Tiki.Contracts.Notifications.NotificationTopics</c> splits email from SMS. A consumer here
/// cares about "something happened to this business" as a single ordered stream — partitioned by
/// business id, so events about one business are never reordered relative to each other — and
/// <see cref="BusinessEvent.EventType"/> is the discriminator a consumer switches on, not the
/// topic name.
/// </remarks>
public static class BusinessTopics
{
    public const string BusinessEvents = "business.events";
}
