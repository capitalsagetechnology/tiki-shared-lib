using System.Linq.Expressions;
using System.Reflection;
using Tiki.Shared.Querydsl.Dto;

namespace Tiki.Shared.Querydsl.Utils;

/// <summary>
/// Builds the predicate expression for a single <see cref="FilterItem"/>, dispatching to
/// the utility for the property's actual type — filter values are always strongly parsed
/// per property type, never treated as <c>dynamic</c>. Never throws for a bad value: an
/// unparseable filter is recorded on <see cref="QuerydslErrorContext"/> and <c>null</c> is
/// returned so the caller can keep validating the rest of the request.
/// </summary>
internal static class ExpressionBuilder
{
    private static readonly HashSet<Type> NumericTypes =
    [
        typeof(byte), typeof(short), typeof(int), typeof(long),
        typeof(float), typeof(double), typeof(decimal),
    ];

    public static Expression? Build(ParameterExpression parameter, PropertyInfo property, FilterItem filter, bool rawTextComparison)
    {
        var access = Expression.Property(parameter, property);
        var propertyType = property.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        var built = underlyingType switch
        {
            _ when underlyingType == typeof(string) =>
                TextExpressionUtils.TryBuild(access, filter.Operation, filter.Value, rawTextComparison, out var expr, out var err)
                    ? (expr, err) : (null, err),
            _ when underlyingType == typeof(bool) =>
                BoolExpressionUtils.TryBuild(access, propertyType, filter.Operation, filter.Value, out var expr, out var err)
                    ? (expr, err) : (null, err),
            _ when underlyingType == typeof(Guid) =>
                GuidExpressionUtils.TryBuild(access, propertyType, filter.Operation, filter.Value, out var expr, out var err)
                    ? (expr, err) : (null, err),
            _ when underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset) =>
                DateTimeExpressionUtils.TryBuild(access, propertyType, filter.Operation, filter.Value, out var expr, out var err)
                    ? (expr, err) : (null, err),
            _ when underlyingType.IsEnum =>
                EnumExpressionUtils.TryBuild(access, propertyType, filter.Operation, filter.Value, out var expr, out var err)
                    ? (expr, err) : (null, err),
            _ when NumericTypes.Contains(underlyingType) =>
                NumberExpressionUtils.TryBuild(access, propertyType, filter.Operation, filter.Value, out var expr, out var err)
                    ? (expr, err) : (null, err),
            _ => ((Expression?)null, $"Property '{property.Name}' has an unsupported type '{underlyingType.Name}' for filtering."),
        };

        if (built.Item1 is not null)
            return built.Item1;

        QuerydslErrorContext.AddError(filter.PropertyName, built.Item2 ?? "Filter value could not be parsed.");
        return null;
    }
}
