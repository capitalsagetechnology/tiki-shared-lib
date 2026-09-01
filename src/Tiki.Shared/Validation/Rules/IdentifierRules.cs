using FluentValidation;

namespace Tiki.Shared.Validation.Rules;

/// <summary>
/// Universal data-shape checks for identifiers. GUID/UUID shape only — this repo has no
/// existing reference-code format of its own to validate against; a service with its own
/// reference-code convention (a settlement reference, a receipt number) validates that
/// shape in its own Application layer, not here.
/// </summary>
public static class IdentifierRules
{
    public static IRuleBuilderOptions<T, string> MustBeGuid<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty().WithMessage("Identifier is required.")
            .Must(value => value is not null && Guid.TryParse(value, out _))
            .WithMessage("Identifier must be a valid GUID.");
}
