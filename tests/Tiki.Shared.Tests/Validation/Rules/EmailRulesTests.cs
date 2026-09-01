using FluentValidation;
using Tiki.Shared.Validation;
using Tiki.Shared.Validation.Rules;
using Xunit;

namespace Tiki.Shared.Tests.Validation.Rules;

public class EmailRulesTests
{
    private sealed class EmailDto
    {
        public string Email { get; init; } = string.Empty;
    }

    private sealed class EmailValidator : TikiValidatorBase<EmailDto>
    {
        public EmailValidator() => RuleFor(x => x.Email).MustBeValidEmail();
    }

    private static readonly EmailValidator Validator = new();

    [Theory]
    [InlineData("wallet-ops@tiki.africa")]
    [InlineData("first.last+tag@sub.example.com")]
    public void Valid_emails_pass(string email)
    {
        var result = Validator.Validate(new EmailDto { Email = email });
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.com")]
    [InlineData("")]
    public void Malformed_emails_are_rejected(string email)
    {
        var result = Validator.Validate(new EmailDto { Email = email });
        Assert.False(result.IsValid);
    }
}
