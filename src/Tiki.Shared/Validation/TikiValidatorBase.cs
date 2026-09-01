using FluentValidation;

namespace Tiki.Shared.Validation;

/// <summary>
/// Base class for every FluentValidation validator in a Tiki service. A new request
/// validator requires no boilerplate beyond the validation rules themselves — inherit
/// this, add <c>RuleFor</c> calls, and register it in DI; <see cref="ValidationBehavior{TRequest,TResponse}"/>
/// picks it up automatically via <see cref="IValidator{T}"/>.
/// </summary>
public abstract class TikiValidatorBase<T> : AbstractValidator<T>;
