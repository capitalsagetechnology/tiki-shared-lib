using FluentValidation;
using Tiki.Shared.Core.Enums;

namespace Tiki.Shared.Validation.Rules;

/// <summary>Universal data-shape checks against the <see cref="CountryCode"/>/<see cref="CurrencyCode"/> vocabulary already defined in <c>Core/Enums</c> — never a duplicate list, never an eligibility rule.</summary>
public static class CountryCurrencyRules
{
    public static IRuleBuilderOptions<T, CountryCode> MustBeKnownCountry<T>(this IRuleBuilder<T, CountryCode> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != CountryCode.Unspecified && Enum.IsDefined(value))
            .WithMessage("Country is not one of the supported country codes.");

    public static IRuleBuilderOptions<T, CurrencyCode> MustBeKnownCurrency<T>(this IRuleBuilder<T, CurrencyCode> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != CurrencyCode.Unspecified && Enum.IsDefined(value))
            .WithMessage("Currency is not one of the supported currency codes.");
}
