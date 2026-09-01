namespace Tiki.Shared.Core.Models;

/// <summary>The shape every list endpoint returns — paired with <see cref="Querydsl.QuerydslExecutor"/>.</summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;

    public static PagedResult<T> Empty(int page, int pageSize) => new()
    {
        Items = [],
        TotalCount = 0,
        Page = page,
        PageSize = pageSize,
    };
}
