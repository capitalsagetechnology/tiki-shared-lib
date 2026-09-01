using System.Linq.Expressions;
using System.Reflection;
using Tiki.Shared.Querydsl.Enums;

namespace Tiki.Shared.Querydsl.Utils;

/// <summary>Builds filter predicates for <see cref="string"/> properties. Case-insensitive by default, unless the property carries <c>[UseRawTextComparison]</c>.</summary>
internal static class TextExpressionUtils
{
    private static readonly MethodInfo ContainsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
    private static readonly MethodInfo StartsWithMethod = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;
    private static readonly MethodInfo EndsWithMethod = typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;
    private static readonly MethodInfo ToLowerMethod = typeof(string).GetMethod(nameof(string.ToLowerInvariant), Type.EmptyTypes)!;
    private static readonly MethodInfo ContainsGenericMethod = typeof(Enumerable).GetMethods()
        .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
        .MakeGenericMethod(typeof(string));

    public static bool TryBuild(
        MemberExpression property, FilterOperation operation, string? rawValue, bool rawComparison,
        out Expression? expression, out string? error)
    {
        error = null;
        expression = null;

        if (operation is FilterOperation.IsNull or FilterOperation.IsNotNull)
        {
            var nullCheck = Expression.Equal(property, Expression.Constant(null, typeof(string)));
            expression = operation == FilterOperation.IsNull ? nullCheck : Expression.Not(nullCheck);
            return true;
        }

        if (operation == FilterOperation.In)
        {
            var values = SplitCsv(rawValue);
            var comparableValues = rawComparison ? values : [.. values.Select(v => v.ToLowerInvariant())];
            Expression comparableProperty = rawComparison ? property : Expression.Call(property, ToLowerMethod);

            expression = Expression.Call(ContainsGenericMethod, Expression.Constant(comparableValues), comparableProperty);
            return true;
        }

        Expression left = rawComparison ? property : Expression.Call(property, ToLowerMethod);
        var comparisonValue = rawValue ?? string.Empty;
        var right = Expression.Constant(rawComparison ? comparisonValue : comparisonValue.ToLowerInvariant());

        expression = operation switch
        {
            FilterOperation.Equals => Expression.Equal(left, right),
            FilterOperation.NotEquals => Expression.NotEqual(left, right),
            FilterOperation.Contains => Expression.Call(left, ContainsMethod, right),
            FilterOperation.StartsWith => Expression.Call(left, StartsWithMethod, right),
            FilterOperation.EndsWith => Expression.Call(left, EndsWithMethod, right),
            _ => null,
        };

        if (expression is not null)
            return true;

        error = $"Operation '{operation}' is not supported for text properties.";
        return false;
    }

    private static string[] SplitCsv(string? rawValue) =>
        (rawValue ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
