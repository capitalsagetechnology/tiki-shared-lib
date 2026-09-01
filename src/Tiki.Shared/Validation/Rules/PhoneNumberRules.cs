using System.Text.RegularExpressions;
using FluentValidation;

namespace Tiki.Shared.Validation.Rules;

/// <summary>Universal data-shape check for phone numbers — E.164 shape only, never a carrier or reachability check.</summary>
public static partial class PhoneNumberRules
{
    // '+' then a non-zero leading digit then 7-14 more digits => 8-15 digits total, matching E.164.
    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")]
    private static partial Regex E164Pattern();

    public static IRuleBuilderOptions<T, string> MustBeE164PhoneNumber<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty().WithMessage("Phone number is required.")
            .Must(value => value is not null && E164Pattern().IsMatch(value))
            .WithMessage("Phone number must be in E.164 format, e.g. +2348012345678.");
}
