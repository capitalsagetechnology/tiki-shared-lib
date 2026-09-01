using System.Linq.Expressions;
using Tiki.Shared.Querydsl.Enums;

namespace Tiki.Shared.Querydsl.Utils;

/// <summary>Builds filter predicates for <see cref="bool"/> properties (nullable or not). Equality only — ordering a boolean is meaningless.</summary>
internal static class BoolExpressionUtils
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
                error = "IsNull/IsNotNull is not supported for a non-nullable bool property.";
                return false;
            }

            var nullCheck = Expression.Equal(property, Expression.Constant(null, propertyType));
            expression = operation == FilterOperation.IsNull ? nullCheck : Expression.Not(nullCheck);
            return true;
        }

        if (!bool.TryParse(rawValue, out var value))
        {
            error = $"Value '{rawValue}' could not be parsed as a boolean.";
            return false;
        }

        var raw = Expression.Constant(value, typeof(bool));
        Expression constant = propertyType == underlyingType ? raw : Expression.Convert(raw, propertyType);

        expression = operation switch
        {
            FilterOperation.Equals => Expression.Equal(property, constant),
            FilterOperation.NotEquals => Expression.NotEqual(property, constant),
            _ => null,
        };

        if (expression is not null)
            return true;

        error = $"Operation '{operation}' is not supported for boolean properties.";
        return false;
    }
}
