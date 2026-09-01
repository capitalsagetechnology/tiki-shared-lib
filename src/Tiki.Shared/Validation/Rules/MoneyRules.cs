using FluentValidation;
using Tiki.Shared.Core.Enums;

namespace Tiki.Shared.Validation.Rules;

/// <summary>
/// Universal data-shape checks for monetary amounts. ISO 4217 minor-unit precision only —
/// never a business rule (transaction limits, fee schedules, FX policy stay with the
/// service that owns them).
/// </summary>
public static class MoneyRules
{
    /// <summary>ISO 4217 minor units (decimal places) for every <see cref="CurrencyCode"/> this repo knows about.</summary>
    private static readonly IReadOnlyDictionary<CurrencyCode, int> MinorUnitsByCurrency = new Dictionary<CurrencyCode, int>
    {
        [CurrencyCode.NGN] = 2,
        [CurrencyCode.GHS] = 2,
        [CurrencyCode.KES] = 2,
        [CurrencyCode.ZAR] = 2,
        [CurrencyCode.USD] = 2,
        [CurrencyCode.GBP] = 2,
        [CurrencyCode.EUR] = 2,
        [CurrencyCode.CAD] = 2,
        [CurrencyCode.RWF] = 0,
        [CurrencyCode.UGX] = 0,
        [CurrencyCode.TZS] = 2,
        [CurrencyCode.XOF] = 0,
        [CurrencyCode.XAF] = 0,
        [CurrencyCode.EGP] = 2,
        [CurrencyCode.MAD] = 2,
    };

    /// <summary>
    /// The amount must be strictly positive, and its decimal precision must not exceed the
    /// selected currency's ISO 4217 minor units — e.g. 3 decimal places is rejected for NGN
    /// (2 minor units) even though the CLR <see cref="decimal"/> itself can represent it.
    /// </summary>
    public static IRuleBuilderOptions<T, decimal> MustBeValidAmount<T>(
        this IRuleBuilder<T, decimal> ruleBuilder, Func<T, CurrencyCode> currencySelector) =>
        ruleBuilder
            .GreaterThan(0m).WithMessage("Amount must be greater than zero.")
            .Must((instance, amount) => HasValidPrecision(amount, currencySelector(instance)))
            .WithMessage((instance, _) =>
            {
                var currency = currencySelector(instance);
                var minorUnits = MinorUnitsByCurrency.GetValueOrDefault(currency, 2);
                return $"Amount has more decimal places than {currency} allows ({minorUnits}).";
            });

    private static bool HasValidPrecision(decimal amount, CurrencyCode currency)
    {
        if (currency == CurrencyCode.Unspecified)
            return false;

        var minorUnits = MinorUnitsByCurrency.GetValueOrDefault(currency, 2);
        var scaled = amount * PowerOfTen(minorUnits);

        // Comparing against Math.Truncate (rather than reading the stored decimal scale via
        // decimal.GetBits) means a value like 3.00m for a 2-minor-unit currency is correctly
        // accepted — the stored scale of a decimal literal is not the same as how many of
        // its decimal places are actually significant.
        return scaled == Math.Truncate(scaled);
    }

    private static decimal PowerOfTen(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
            result *= 10m;
        return result;
    }
}
