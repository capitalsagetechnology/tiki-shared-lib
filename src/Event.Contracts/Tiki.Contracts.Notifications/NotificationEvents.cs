using Tiki.Shared.Core.Events;

namespace Tiki.Contracts.Notifications;

/// <summary>
/// Fields every notification event carries, on top of the platform envelope.
/// </summary>
/// <remarks>
/// Derives from <see cref="BaseEvent"/> so a notification carries the same trace id, source
/// service and subject id as every other event on the platform — which is what lets a delivered
/// email be traced back to the request that asked for it.
/// </remarks>
public abstract record NotificationEvent : BaseEvent
{
    /// <summary>The tenant this notification belongs to, where there is one.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    /// Collapses duplicate sends across retries and redeliveries: two events with the same key
    /// deliver at most one message.
    /// </summary>
    /// <remarks>
    /// Required rather than optional, and the producer's responsibility, because only the
    /// producer knows what "the same notification" means. Kafka delivery is at-least-once, so
    /// without this a broker redelivery is a second email to a real person — and for an
    /// invitation or a password reset, a duplicate is not merely untidy: two live reset links
    /// where the user expected one is a support call at best.
    /// </remarks>
    public required string IdempotencyKey { get; init; }
}

/// <summary>
/// A request to deliver one templated email. The producer picks the template and supplies its
/// model; rendering and transport are entirely the Notification service's business.
/// </summary>
public sealed record EmailRequested : NotificationEvent
{
    public required string To { get; init; }

    public string? ToDisplayName { get; init; }

    /// <summary>One of <see cref="EmailTemplates"/>.</summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// The template's model. Deliberately a flat string dictionary — it crosses a wire and gets
    /// substituted into markup, so anything richer would need a schema per template and a
    /// versioning story for each.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Model { get; init; }

    /// <summary>Overrides the configured default sender. Rarely needed.</summary>
    public string? FromOverride { get; init; }

    /// <summary>
    /// The client the action link should point at, e.g. <c>https://backoffice.tiki.africa</c>.
    /// </summary>
    /// <remarks>
    /// Supplied by the caller because only it knows which client a given user is being sent to —
    /// the backoffice, and in future a customer or partner app. The notification service still
    /// decides whether to honour it: the value is checked against
    /// <c>Notification:ClientLinks:AllowedOrigins</c> and a message naming an unlisted origin is
    /// rejected rather than sent.
    ///
    /// <para>
    /// That check is the whole point. Without it this field is a producer-chosen destination, and
    /// anything able to publish to the topic could send a Tiki-branded invitation whose button
    /// lands on a page it controls — with a real recipient's name on it. The allow-list keeps the
    /// choice with the caller while keeping the set of possible answers in deployment config.
    /// </para>
    ///
    /// <para>Null falls back to the configured default client.</para>
    /// </remarks>
    public string? ClientBaseUrl { get; init; }
}

/// <summary>A request to deliver one SMS.</summary>
public sealed record SmsRequested : NotificationEvent
{
    /// <summary>E.164, e.g. <c>+2348012345678</c>.</summary>
    public required string To { get; init; }

    /// <summary>
    /// The message body, already composed. SMS has no templating layer: a message is one short
    /// string with no markup, and a template engine would add a failure mode for no benefit.
    /// </summary>
    public required string Body { get; init; }
}

/// <summary>
/// The template catalogue. Ids match the file names under the Notification service's
/// <c>Templates/</c> directory.
/// </summary>
public static class EmailTemplates
{
    /// <summary>Invitation to join the platform — carries the link that sets a first password.</summary>
    public const string TeamMemberInvitation = "team-member-invitation";

    /// <summary>Sent once an invited member has set their password and can sign in.</summary>
    public const string WelcomeOnboarded = "welcome-onboarded";

    /// <summary>Password reset link, requested by the user.</summary>
    public const string PasswordReset = "password-reset";

    /// <summary>Confirmation that a password was changed — the "was this you?" safety net.</summary>
    public const string PasswordChanged = "password-changed";

    /// <summary>A user was granted access to a tenant.</summary>
    public const string TenantAccessGranted = "tenant-access-granted";

    public const string AccountSuspended = "account-suspended";

    public const string AccountReinstated = "account-reinstated";

    /// <summary>A one-time verification/login code, delivered by email.</summary>
    public const string OtpCode = "otp-code";
}

/// <summary>
/// Model keys the templates read. Constants rather than loose strings because a producer and a
/// template that disagree about a key produce an email with a blank button, and nothing fails.
/// </summary>
public static class EmailModelKeys
{
    public const string FirstName = "firstName";
    public const string FullName = "fullName";
    public const string Email = "email";

    /// <summary>The absolute URL the call-to-action button points at.</summary>
    public const string ActionUrl = "actionUrl";

    /// <summary>How long the link in <see cref="ActionUrl"/> remains valid, e.g. "24 hours".</summary>
    public const string ExpiresIn = "expiresIn";

    public const string TenantName = "tenantName";
    public const string RoleName = "roleName";
    public const string InvitedByName = "invitedByName";

    /// <summary>Where the request came from, for the "was this you?" line on security emails.</summary>
    public const string RequestIpAddress = "requestIpAddress";
    public const string RequestedAt = "requestedAt";

    /// <summary>The one-time code itself, for <see cref="EmailTemplates.OtpCode"/>.</summary>
    public const string Code = "code";
}
