using FluentValidation;
using Tiki.Shared.Core.Enums;
using Tiki.Shared.Validation;
using Tiki.Shared.Validation.Rules;
using Xunit;

namespace Tiki.Shared.Tests.Validation.Rules;

public class CountryCurrencyRulesTests
{
    private sealed class CountryCurrencyDto
    {
        public CountryCode Country { get; init; }
        public CurrencyCode Currency { get; init; }
    }

    private sealed class CountryCurrencyValidator : TikiValidatorBase<CountryCurrencyDto>
    {
        public CountryCurrencyValidator()
        {
            RuleFor(x => x.Country).MustBeKnownCountry();
            RuleFor(x => x.Currency).MustBeKnownCurrency();
        }
    }

    private static readonly CountryCurrencyValidator Validator = new();

    [Fact]
    public void Known_country_and_currency_pass()
    {
        var result = Validator.Validate(new CountryCurrencyDto { Country = CountryCode.NG, Currency = CurrencyCode.NGN });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Unspecified_country_is_rejected()
    {
        var result = Validator.Validate(new CountryCurrencyDto { Country = CountryCode.Unspecified, Currency = CurrencyCode.NGN });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CountryCurrencyDto.Country));
    }

    [Fact]
    public void Unspecified_currency_is_rejected()
    {
        var result = Validator.Validate(new CountryCurrencyDto { Country = CountryCode.NG, Currency = CurrencyCode.Unspecified });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CountryCurrencyDto.Currency));
    }

    [Fact]
    public void Undefined_enum_value_is_rejected()
    {
        var result = Validator.Validate(new CountryCurrencyDto { Country = (CountryCode)9999, Currency = CurrencyCode.NGN });
        Assert.False(result.IsValid);
    }
}
