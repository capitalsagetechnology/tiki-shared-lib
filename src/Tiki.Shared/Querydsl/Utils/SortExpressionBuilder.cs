using System.Linq.Expressions;
using System.Reflection;
using Tiki.Shared.Querydsl.Dto;
using Tiki.Shared.Querydsl.Enums;

namespace Tiki.Shared.Querydsl.Utils;

/// <summary>Applies an ordered <c>OrderBy</c>/<c>ThenBy</c> chain built via reflection, since each sort key's property type differs.</summary>
internal static class SortExpressionBuilder
{
    private static readonly MethodInfo OrderByMethod = GetQueryableMethod(nameof(Queryable.OrderBy));
    private static readonly MethodInfo OrderByDescendingMethod = GetQueryableMethod(nameof(Queryable.OrderByDescending));
    private static readonly MethodInfo ThenByMethod = GetQueryableMethod(nameof(Queryable.ThenBy));
    private static readonly MethodInfo ThenByDescendingMethod = GetQueryableMethod(nameof(Queryable.ThenByDescending));

    public static IQueryable<T> ApplySort<T>(IQueryable<T> source, IReadOnlyList<SortItem> sorts)
    {
        var parameter = Expression.Parameter(typeof(T), "entity");
        var isFirst = true;
        var result = source;

        foreach (var sort in sorts)
        {
            var property = PropertyUtils.FindFilterableProperty(typeof(T), sort.PropertyName);
            if (property is null)
            {
                var reason = PropertyUtils.IsIgnored(typeof(T), sort.PropertyName)
                    ? $"Property '{sort.PropertyName}' is excluded from sorting."
                    : $"Property '{sort.PropertyName}' does not exist on {typeof(T).Name}.";
                QuerydslErrorContext.AddError(sort.PropertyName, reason);
                continue;
            }

            var keySelector = Expression.Lambda(Expression.Property(parameter, property), parameter);

            var method = (isFirst, sort.Direction) switch
            {
                (true, SortDirection.Descending) => OrderByDescendingMethod,
                (true, _) => OrderByMethod,
                (false, SortDirection.Descending) => ThenByDescendingMethod,
                (false, _) => ThenByMethod,
            };

            var genericMethod = method.MakeGenericMethod(typeof(T), property.PropertyType);
            result = (IQueryable<T>)genericMethod.Invoke(null, [result, keySelector])!;
            isFirst = false;
        }

        return result;
    }

    private static MethodInfo GetQueryableMethod(string name) =>
        typeof(Queryable).GetMethods().First(m => m.Name == name && m.GetParameters().Length == 2);
}
