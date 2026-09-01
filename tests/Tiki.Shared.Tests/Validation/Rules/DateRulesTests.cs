using FluentValidation;
using Tiki.Shared.Validation;
using Tiki.Shared.Validation.Rules;
using Xunit;

namespace Tiki.Shared.Tests.Validation.Rules;

public class DateRulesTests
{
    private sealed class DateDto
    {
        public DateTimeOffset Date { get; init; }
    }

    private sealed class NotInFutureValidator : TikiValidatorBase<DateDto>
    {
        public NotInFutureValidator() => RuleFor(x => x.Date).MustNotBeInTheFuture();
    }

    private sealed class DateOfBirthValidator : TikiValidatorBase<DateDto>
    {
        public DateOfBirthValidator() => RuleFor(x => x.Date).MustBeValidDateOfBirth();
    }

    private sealed class RangeValidator : TikiValidatorBase<DateDto>
    {
        public RangeValidator() =>
            RuleFor(x => x.Date).MustBeWithinRange(
                new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Past_date_is_not_in_the_future()
    {
        var result = new NotInFutureValidator().Validate(new DateDto { Date = DateTimeOffset.UtcNow.AddDays(-1) });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Future_date_is_rejected()
    {
        var result = new NotInFutureValidator().Validate(new DateDto { Date = DateTimeOffset.UtcNow.AddDays(1) });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Exactly_eighteen_years_old_today_passes_the_default_minimum()
    {
        var eighteenYearsAgo = DateTimeOffset.UtcNow.Date.AddYears(-18);
        var result = new DateOfBirthValidator().Validate(new DateDto { Date = eighteenYearsAgo });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Seventeen_years_old_fails_the_default_minimum()
    {
        var seventeenYearsAgo = DateTimeOffset.UtcNow.Date.AddYears(-17);
        var result = new DateOfBirthValidator().Validate(new DateDto { Date = seventeenYearsAgo });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void A_future_date_of_birth_is_rejected()
    {
        var result = new DateOfBirthValidator().Validate(new DateDto { Date = DateTimeOffset.UtcNow.AddDays(1) });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void An_implausibly_old_date_of_birth_is_rejected()
    {
        var result = new DateOfBirthValidator().Validate(new DateDto { Date = DateTimeOffset.UtcNow.Date.AddYears(-130) });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Date_inside_the_range_passes()
    {
        var result = new RangeValidator().Validate(new DateDto { Date = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero) });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Date_outside_the_range_is_rejected()
    {
        var result = new RangeValidator().Validate(new DateDto { Date = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) });
        Assert.False(result.IsValid);
    }
}
