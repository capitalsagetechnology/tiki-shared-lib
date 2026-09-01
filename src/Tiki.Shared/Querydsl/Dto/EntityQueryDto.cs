namespace Tiki.Shared.Querydsl.Dto;

/// <summary>The request shape every paginated list endpoint accepts. <see cref="Page"/> is 1-based.</summary>
public sealed class EntityQueryDto
{
    public IReadOnlyList<FilterItem> Filters { get; init; } = [];
    public IReadOnlyList<SortItem> Sorts { get; init; } = [];
    public int Page { get; init; } = 1;

    /// <summary>Capped server-side at <see cref="QuerydslExecutor.MaxPageSize"/> regardless of what is requested — there is no unbounded escape hatch.</summary>
    public int PageSize { get; init; } = 25;
}
