namespace Tiki.Shared.Querydsl.Dto;

/// <summary>Request shape for a filter-only count — no sort, no page, just "how many match".</summary>
public sealed class EntityCountDto
{
    public IReadOnlyList<FilterItem> Filters { get; init; } = [];
}
