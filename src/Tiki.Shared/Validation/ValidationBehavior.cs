using FluentValidation;
using Tiki.Shared.Results;

namespace Tiki.Shared.Validation;

/// <summary>Continuation delegate for a pipeline step — the same shape as MediatR's <c>RequestHandlerDelegate&lt;TResponse&gt;</c>.</summary>
public delegate Task<TResponse> TikiRequestHandlerDelegate<TResponse>();

/// <summary>
/// A single pipeline step around a request handler. Deliberately shaped identically to
/// MediatR's <c>IPipelineBehavior&lt;TRequest,TResponse&gt;</c> — a service already on
/// MediatR can implement it with a one-line adapter — without this package taking a hard
/// dependency on any specific mediator library, matching this package's rule against
/// framework-level indirection.
/// </summary>
public interface ITikiPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    Task<TResponse> Handle(TRequest request, TikiRequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>
/// Runs every registered <see cref="IValidator{T}"/> for <typeparamref name="TRequest"/>
/// before the handler executes. A failing validator short-circuits to a
/// <see cref="Result"/>/<see cref="Result{T}"/> failure — <typeparamref name="TResponse"/>
/// must be one of those two types. No boilerplate is required beyond the validation rules
/// themselves and registering the validator in DI.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : ITikiPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request, TikiRequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var validatorList = validators as IValidator<TRequest>[] ?? [.. validators];
        if (validatorList.Length == 0)
            return await next();

        var validationResults = await Task.WhenAll(
            validatorList.Select(validator => validator.ValidateAsync(request, cancellationToken)));

        var failures = validationResults
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var errors = failures
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(f => f.ErrorMessage).ToArray());

        return CreateValidationFailure(errors);
    }

    private static TResponse CreateValidationFailure(IReadOnlyDictionary<string, string[]> errors)
    {
        var error = Error.Validation("tiki.validation", "One or more validation errors occurred.");
        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
            return (TResponse)(object)Result.Failure(error);

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];
            var failureMethod = typeof(Result)
                .GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!
                .MakeGenericMethod(valueType);
            return (TResponse)failureMethod.Invoke(null, [error])!;
        }

        throw new InvalidOperationException(
            $"{responseType.Name} is not supported by {nameof(ValidationBehavior<TRequest, TResponse>)} — " +
            $"TResponse must be {nameof(Result)} or {nameof(Result)}<T>. Validation errors: " +
            string.Join("; ", errors.Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value)}")));
    }
}
