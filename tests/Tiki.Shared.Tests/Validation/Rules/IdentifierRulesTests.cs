using FluentValidation;
using Tiki.Shared.Validation;
using Tiki.Shared.Validation.Rules;
using Xunit;

namespace Tiki.Shared.Tests.Validation.Rules;

public class IdentifierRulesTests
{
    private sealed class IdentifierDto
    {
        public string Id { get; init; } = string.Empty;
    }

    private sealed class IdentifierValidator : TikiValidatorBase<IdentifierDto>
    {
        public IdentifierValidator() => RuleFor(x => x.Id).MustBeGuid();
    }

    private static readonly IdentifierValidator Validator = new();

    [Fact]
    public void Valid_guid_passes()
    {
        var result = Validator.Validate(new IdentifierDto { Id = Guid.NewGuid().ToString() });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Non_guid_string_is_rejected()
    {
        var result = Validator.Validate(new IdentifierDto { Id = "not-a-guid" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_identifier_is_rejected()
    {
        var result = Validator.Validate(new IdentifierDto { Id = "" });
        Assert.False(result.IsValid);
    }
}
