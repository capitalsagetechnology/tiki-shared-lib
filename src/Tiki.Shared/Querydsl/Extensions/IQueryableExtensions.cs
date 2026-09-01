using System.Linq.Expressions;
using Tiki.Shared.Querydsl.Dto;
using Tiki.Shared.Querydsl.Enums;
using Tiki.Shared.Querydsl.Utils;

namespace Tiki.Shared.Querydsl.Extensions;

/// <summary>
/// Composable, lower-level building blocks over any <see cref="IQueryable{T}"/> — pure
/// LINQ-expression-building, no dependency on <c>Microsoft.EntityFrameworkCore</c> anywhere
/// in this package. <see cref="QuerydslExecutor"/> composes these into the full
/// filter → sort → paginate pipeline; use them directly if you only need one stage.
/// </summary>
public static class IQueryableExtensions
{
    /// <summary>
    /// Applies every filter, combined per each item's <see cref="FilterItem.Conjunction"/>
    /// with the next. A filter naming an unknown or <c>[QuerydslIgnore]</c>d property, or
    /// carrying an unparseable value, is recorded on <see cref="QuerydslErrorContext"/>
    /// rather than applied or thrown — check <see cref="QuerydslErrorContext.Current"/>
    /// after calling this before executing the query.
    /// </summary>
    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> source, IReadOnlyList<FilterItem> filters)
    {
        if (filters.Count == 0)
            return source;

        var parameter = Expression.Parameter(typeof(T), "entity");
        Expression? combined = null;
        var pendingConjunction = QueryConjunction.And;

        foreach (var filter in filters)
        {
            var property = PropertyUtils.FindFilterableProperty(typeof(T), filter.PropertyName);
            if (property is null)
            {
                var reason = PropertyUtils.IsIgnored(typeof(T), filter.PropertyName)
                    ? $"Property '{filter.PropertyName}' is excluded from filtering."
                    : $"Property '{filter.PropertyName}' does not exist on {typeof(T).Name}.";
                QuerydslErrorContext.AddError(filter.PropertyName, reason);
                continue;
            }

            var rawTextComparison = PropertyUtils.UsesRawTextComparison(property);
            var expression = ExpressionBuilder.Build(parameter, property, filter, rawTextComparison);
            if (expression is null)
                continue; // error already recorded by ExpressionBuilder

            combined = combined is null
                ? expression
                : pendingConjunction == QueryConjunction.Or
                    ? Expression.OrElse(combined, expression)
                    : Expression.AndAlso(combined, expression);

            pendingConjunction = filter.Conjunction;
        }

        if (combined is null)
            return source;

        var lambda = Expression.Lambda<Func<T, bool>>(combined, parameter);
        return source.Where(lambda);
    }

    /// <summary>Applies an <c>OrderBy</c>/<c>ThenBy</c> chain in the order supplied.</summary>
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> source, IReadOnlyList<SortItem> sorts) =>
        sorts.Count == 0 ? source : SortExpressionBuilder.ApplySort(source, sorts);

    /// <summary><paramref name="page"/> is 1-based.</summary>
    public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> source, int page, int pageSize) =>
        source.Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize);
}
