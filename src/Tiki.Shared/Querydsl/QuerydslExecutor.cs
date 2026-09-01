using Tiki.Shared.Core.Models;
using Tiki.Shared.Querydsl.Dto;
using Tiki.Shared.Querydsl.Extensions;

namespace Tiki.Shared.Querydsl;

/// <summary>Outcome of <see cref="QuerydslExecutor.Execute{T}"/> — either a page of results or the validation failures that stopped the query from running.</summary>
public sealed class QuerydslResult<T>
{
    public bool IsValid { get; }
    public PagedResult<T>? Value { get; }
    public IReadOnlyList<QuerydslFieldError> Errors { get; }

    private QuerydslResult(bool isValid, PagedResult<T>? value, IReadOnlyList<QuerydslFieldError> errors)
    {
        IsValid = isValid;
        Value = value;
        Errors = errors;
    }

    public static QuerydslResult<T> Valid(PagedResult<T> value) => new(true, value, []);
    public static QuerydslResult<T> Invalid(IReadOnlyList<QuerydslFieldError> errors) => new(false, null, errors);
}

/// <summary>
/// Dynamic filter/sort/paginate over any <see cref="IQueryable{T}"/> the caller supplies —
/// zero dependency on <c>Microsoft.EntityFrameworkCore</c> in this package, unit-testable
/// against an in-memory <c>IQueryable&lt;T&gt;</c> with no database involved. Every list
/// query is paginated: <see cref="MaxPageSize"/> is a hard ceiling applied regardless of
/// what the caller requests, and there is no "give me everything" escape hatch.
/// </summary>
public static class QuerydslExecutor
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    /// <summary>
    /// Runs the full filter → sort → paginate pipeline. If any filter or sort in
    /// <paramref name="query"/> fails validation, the underlying query is never executed —
    /// every failure is returned together via <see cref="QuerydslResult{T}.Errors"/>,
    /// not just the first one encountered.
    /// </summary>
    public static QuerydslResult<T> Execute<T>(IQueryable<T> source, EntityQueryDto query)
    {
        using var _ = QuerydslErrorContext.Begin();

        var filtered = source.ApplyFilters(query.Filters);
        var sorted = filtered.ApplySort(query.Sorts);

        var errors = QuerydslErrorContext.Current;
        if (errors.Count > 0)
            return QuerydslResult<T>.Invalid(errors);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? DefaultPageSize : query.PageSize, 1, MaxPageSize);

        var totalCount = sorted.Count();
        var items = sorted.ApplyPaging(page, pageSize).ToList();

        return QuerydslResult<T>.Valid(new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        });
    }

    /// <summary>Filter-only count, for endpoints that need "how many match" without materializing a page.</summary>
    public static QuerydslResult<int> Count<T>(IQueryable<T> source, EntityCountDto query)
    {
        using var _ = QuerydslErrorContext.Begin();

        var filtered = source.ApplyFilters(query.Filters);

        var errors = QuerydslErrorContext.Current;
        if (errors.Count > 0)
            return QuerydslResult<int>.Invalid(errors);

        var count = filtered.Count();
        return QuerydslResult<int>.Valid(new PagedResult<int>
        {
            Items = [count],
            TotalCount = count,
            Page = 1,
            PageSize = 1,
        });
    }
}
