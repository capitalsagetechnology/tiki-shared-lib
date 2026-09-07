namespace Tiki.Contracts.Notifications;

/// <summary>
/// The topics the Notification service owns.
/// </summary>
/// <remarks>
/// Suffixes, not full names — the deployment-wide prefix (<c>Tiki:Messaging:TopicPrefix</c>) is
/// prepended at runtime, which is what lets several environments share one broker without
/// reading each other's traffic.
/// </remarks>
public static class NotificationTopics
{
    /// <summary>A request to deliver one templated email.</summary>
    public const string EmailRequested = "notification.email.requested";

    /// <summary>Emails that failed every retry. Kept far longer than live traffic so they can be replayed by hand.</summary>
    public const string EmailDeadLetter = "notification.email.requested.dlq";

    /// <summary>A request to deliver one SMS.</summary>
    public const string SmsRequested = "notification.sms.requested";

    public const string SmsDeadLetter = "notification.sms.requested.dlq";
}
