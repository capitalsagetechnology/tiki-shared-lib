using FluentValidation;

namespace Tiki.Shared.Validation.Rules;

/// <summary>Universal data-shape check for email addresses — standard shape validation, never a mailbox-existence or deliverability check.</summary>
public static class EmailRules
{
    public static IRuleBuilderOptions<T, string> MustBeValidEmail<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid email address.");
}
