using FluentValidation;

namespace Tiki.Shared.Validation.Rules;

/// <summary>Universal data-shape checks for dates — bounds only, never a business rule about what a date means.</summary>
public static class DateRules
{
    public static IRuleBuilderOptions<T, DateTimeOffset> MustNotBeInTheFuture<T>(this IRuleBuilder<T, DateTimeOffset> ruleBuilder) =>
        ruleBuilder
            .Must(value => value <= DateTimeOffset.UtcNow)
            .WithMessage("Date must not be in the future.");

    /// <summary>Not in the future, and implies an age within <paramref name="minimumAge"/>–<paramref name="maximumAge"/> years, as of today.</summary>
    public static IRuleBuilderOptions<T, DateTimeOffset> MustBeValidDateOfBirth<T>(
        this IRuleBuilder<T, DateTimeOffset> ruleBuilder, int minimumAge = 18, int maximumAge = 120) =>
        ruleBuilder
            .Must(dob => dob <= DateTimeOffset.UtcNow)
            .WithMessage("Date of birth must not be in the future.")
            .Must(dob => CalculateAge(dob, DateTimeOffset.UtcNow) >= minimumAge)
            .WithMessage($"Must be at least {minimumAge} years old.")
            .Must(dob => CalculateAge(dob, DateTimeOffset.UtcNow) <= maximumAge)
            .WithMessage($"Date of birth implies an age greater than {maximumAge}, which is not plausible.");

    public static IRuleBuilderOptions<T, DateTimeOffset> MustBeWithinRange<T>(
        this IRuleBuilder<T, DateTimeOffset> ruleBuilder, DateTimeOffset earliest, DateTimeOffset latest) =>
        ruleBuilder
            .Must(value => value >= earliest && value <= latest)
            .WithMessage($"Date must be between {earliest:yyyy-MM-dd} and {latest:yyyy-MM-dd}.");

    private static int CalculateAge(DateTimeOffset dateOfBirth, DateTimeOffset asOf)
    {
        var age = asOf.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > asOf.Date.AddYears(-age))
            age--;

        return age;
    }
}
