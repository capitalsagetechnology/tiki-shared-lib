namespace Tiki.Shared.Core.Attributes;

/// <summary>
/// Marks the property on a Kafka message (a <see cref="Events.BaseEvent"/> subtype) or an
/// HTTP request DTO that uniquely identifies the operation for idempotency purposes.
/// Consumers built on <see cref="Messaging.TikiConsumerBackgroundService{TMessage}"/> re-invoke
/// <c>HandleAsync</c> under at-least-once delivery, so the owning service must key its own
/// idempotency store off this property — this attribute only documents which one that is.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class IdempotencyKeyAttribute : Attribute;
