namespace Tiki.Shared.Querydsl;

/// <summary>A single filter/sort validation failure, naming the offending property.</summary>
public sealed record QuerydslFieldError(string PropertyName, string Message);

/// <summary>
/// <see cref="AsyncLocal{T}"/>-backed error accumulator scoped to one <see cref="QuerydslExecutor.Execute{T}"/>
/// call. Every filter and sort in a request is validated and every failure collected here
/// — a bad filter never fails the whole batch on first error, and never leaks an exception
/// with internal type detail to the caller.
/// </summary>
public static class QuerydslErrorContext
{
    private static readonly AsyncLocal<List<QuerydslFieldError>?> Errors = new();

    /// <summary>Starts a new error-collection scope. Dispose to clear it (nesting is not supported).</summary>
    public static IDisposable Begin()
    {
        Errors.Value = [];
        return new Scope();
    }

    public static void AddError(string propertyName, string message) =>
        (Errors.Value ??= []).Add(new QuerydslFieldError(propertyName, message));

    public static IReadOnlyList<QuerydslFieldError> Current => Errors.Value ?? [];

    private sealed class Scope : IDisposable
    {
        public void Dispose() => Errors.Value = null;
    }
}
