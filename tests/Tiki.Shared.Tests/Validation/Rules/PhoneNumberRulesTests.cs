using FluentValidation;
using Tiki.Shared.Validation;
using Tiki.Shared.Validation.Rules;
using Xunit;

namespace Tiki.Shared.Tests.Validation.Rules;

public class PhoneNumberRulesTests
{
    private sealed class PhoneDto
    {
        public string PhoneNumber { get; init; } = string.Empty;
    }

    private sealed class PhoneValidator : TikiValidatorBase<PhoneDto>
    {
        public PhoneValidator() => RuleFor(x => x.PhoneNumber).MustBeE164PhoneNumber();
    }

    private static readonly PhoneValidator Validator = new();

    [Theory]
    [InlineData("+2348012345678")]
    [InlineData("+14155552671")]
    [InlineData("+254712345678")]
    public void Valid_e164_numbers_pass(string phoneNumber)
    {
        var result = Validator.Validate(new PhoneDto { PhoneNumber = phoneNumber });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Missing_leading_plus_is_rejected()
    {
        var result = Validator.Validate(new PhoneDto { PhoneNumber = "2348012345678" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Leading_zero_after_plus_is_rejected()
    {
        var result = Validator.Validate(new PhoneDto { PhoneNumber = "+0123456789" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Fewer_than_eight_digits_is_rejected()
    {
        var result = Validator.Validate(new PhoneDto { PhoneNumber = "+2348" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void More_than_fifteen_digits_is_rejected()
    {
        var result = Validator.Validate(new PhoneDto { PhoneNumber = "+1234567890123456" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_phone_number_is_rejected()
    {
        var result = Validator.Validate(new PhoneDto { PhoneNumber = "" });
        Assert.False(result.IsValid);
    }
}
