using System.Globalization;
using System.Linq.Expressions;
using Tiki.Shared.Querydsl.Enums;

namespace Tiki.Shared.Querydsl.Utils;

/// <summary>Builds filter predicates for <see cref="DateTime"/>/<see cref="DateTimeOffset"/> properties (nullable or not).</summary>
internal static class DateTimeExpressionUtils
{
    public static bool TryBuild(
        MemberExpression property, Type propertyType, FilterOperation operation, string? rawValue,
        out Expression? expression, out string? error)
    {
        error = null;
        expression = null;
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (operation is FilterOperation.IsNull or FilterOperation.IsNotNull)
        {
            if (Nullable.GetUnderlyingType(propertyType) is null)
            {
                error = "IsNull/IsNotNull is not supported for a non-nullable date property.";
                return false;
            }

            var nullCheck = Expression.Equal(property, Expression.Constant(null, propertyType));
            expression = operation == FilterOperation.IsNull ? nullCheck : Expression.Not(nullCheck);
            return true;
        }

        if (!TryParse(underlyingType, rawValue, out var parsed))
        {
            error = $"Value '{rawValue}' could not be parsed as {underlyingType.Name}.";
            return false;
        }

        var constant = BuildConstant(parsed!, propertyType, underlyingType);

        expression = operation switch
        {
            FilterOperation.Equals => Expression.Equal(property, constant),
            FilterOperation.NotEquals => Expression.NotEqual(property, constant),
            FilterOperation.GreaterThan => Expression.GreaterThan(property, constant),
            FilterOperation.GreaterThanOrEqual => Expression.GreaterThanOrEqual(property, constant),
            FilterOperation.LessThan => Expression.LessThan(property, constant),
            FilterOperation.LessThanOrEqual => Expression.LessThanOrEqual(property, constant),
            _ => null,
        };

        if (expression is not null)
            return true;

        error = $"Operation '{operation}' is not supported for date properties.";
        return false;
    }

    private static bool TryParse(Type underlyingType, string? rawValue, out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        if (underlyingType == typeof(DateTimeOffset))
        {
            if (!DateTimeOffset.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
                return false;
            value = dto;
            return true;
        }

        if (underlyingType == typeof(DateTime))
        {
            if (!DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
                return false;
            value = dt;
            return true;
        }

        return false;
    }

    private static Expression BuildConstant(object value, Type propertyType, Type underlyingType)
    {
        var raw = Expression.Constant(value, underlyingType);
        return propertyType == underlyingType ? raw : Expression.Convert(raw, propertyType);
    }
}
