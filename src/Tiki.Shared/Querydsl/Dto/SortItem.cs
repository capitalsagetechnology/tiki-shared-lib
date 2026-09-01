using Tiki.Shared.Querydsl.Enums;

namespace Tiki.Shared.Querydsl.Dto;

public sealed class SortItem
{
    public required string PropertyName { get; init; }
    public SortDirection Direction { get; init; } = SortDirection.Ascending;
}
