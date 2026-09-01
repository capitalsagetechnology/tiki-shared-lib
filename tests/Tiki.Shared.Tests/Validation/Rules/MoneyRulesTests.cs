using FluentValidation;
using Tiki.Shared.Core.Enums;
using Tiki.Shared.Validation;
using Tiki.Shared.Validation.Rules;
using Xunit;

namespace Tiki.Shared.Tests.Validation.Rules;

public class MoneyRulesTests
{
    private sealed class MoneyDto
    {
        public decimal Amount { get; init; }
        public CurrencyCode CurrencyCode { get; init; }
    }

    private sealed class MoneyValidator : TikiValidatorBase<MoneyDto>
    {
        public MoneyValidator() => RuleFor(x => x.Amount).MustBeValidAmount(x => x.CurrencyCode);
    }

    private static readonly MoneyValidator Validator = new();

    [Fact]
    public void Zero_amount_is_rejected()
    {
        var result = Validator.Validate(new MoneyDto { Amount = 0m, CurrencyCode = CurrencyCode.NGN });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Negative_amount_is_rejected()
    {
        var result = Validator.Validate(new MoneyDto { Amount = -5m, CurrencyCode = CurrencyCode.NGN });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Two_decimal_places_is_valid_for_ngn()
    {
        var result = Validator.Validate(new MoneyDto { Amount = 1500.50m, CurrencyCode = CurrencyCode.NGN });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Three_decimal_places_is_rejected_for_ngn()
    {
        var result = Validator.Validate(new MoneyDto { Amount = 1500.505m, CurrencyCode = CurrencyCode.NGN });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("NGN"));
    }

    [Fact]
    public void Trailing_zero_decimal_places_do_not_falsely_fail()
    {
        // 3.00m has a stored decimal scale of 2 despite being a whole number — the rule
        // must judge significant precision, not decimal.GetBits' stored scale.
        var result = Validator.Validate(new MoneyDto { Amount = 3.00m, CurrencyCode = CurrencyCode.USD });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Zero_minor_unit_currency_rejects_any_fractional_amount()
    {
        var result = Validator.Validate(new MoneyDto { Amount = 100.50m, CurrencyCode = CurrencyCode.RWF });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Zero_minor_unit_currency_accepts_a_whole_number()
    {
        var result = Validator.Validate(new MoneyDto { Amount = 100m, CurrencyCode = CurrencyCode.RWF });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Unspecified_currency_is_rejected_regardless_of_amount()
    {
        var result = Validator.Validate(new MoneyDto { Amount = 100m, CurrencyCode = CurrencyCode.Unspecified });
        Assert.False(result.IsValid);
    }
}
