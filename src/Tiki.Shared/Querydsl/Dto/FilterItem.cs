using Tiki.Shared.Querydsl.Enums;

namespace Tiki.Shared.Querydsl.Dto;

/// <summary>A single filter clause. <see cref="Conjunction"/> is how this filter combines with the NEXT one in the list.</summary>
public sealed class FilterItem
{
    public required string PropertyName { get; init; }
    public required FilterOperation Operation { get; init; }

    /// <summary>
    /// Raw, unparsed value — comma-separated for <see cref="FilterOperation.In"/>, unused
    /// for <see cref="FilterOperation.IsNull"/>/<see cref="FilterOperation.IsNotNull"/>.
    /// Parsed per the target property's actual type; never treated as <c>dynamic</c>.
    /// </summary>
    public string? Value { get; init; }

    public QueryConjunction Conjunction { get; init; } = QueryConjunction.And;
}
