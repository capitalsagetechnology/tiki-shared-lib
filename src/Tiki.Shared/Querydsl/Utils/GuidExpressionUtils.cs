using System.Linq.Expressions;
using Tiki.Shared.Querydsl.Enums;

namespace Tiki.Shared.Querydsl.Utils;

/// <summary>Builds filter predicates for <see cref="Guid"/> properties (nullable or not). Supports equality and <c>In</c> only — ordering a guid is meaningless.</summary>
internal static class GuidExpressionUtils
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
                error = "IsNull/IsNotNull is not supported for a non-nullable guid property.";
                return false;
            }

            var nullCheck = Expression.Equal(property, Expression.Constant(null, propertyType));
            expression = operation == FilterOperation.IsNull ? nullCheck : Expression.Not(nullCheck);
            return true;
        }

        if (operation == FilterOperation.In)
        {
            var rawValues = (rawValue ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var parsed = new Guid[rawValues.Length];
            for (var i = 0; i < rawValues.Length; i++)
            {
                if (!Guid.TryParse(rawValues[i], out parsed[i]))
                {
                    error = $"Value '{rawValues[i]}' could not be parsed as a guid.";
                    return false;
                }
            }

            var containsMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(Guid));

            Expression valueExpr = propertyType == underlyingType ? property : Expression.Convert(property, underlyingType);
            expression = Expression.Call(containsMethod, Expression.Constant(parsed), valueExpr);
            return true;
        }

        if (!Guid.TryParse(rawValue, out var guidValue))
        {
            error = $"Value '{rawValue}' could not be parsed as a guid.";
            return false;
        }

        var raw = Expression.Constant(guidValue, typeof(Guid));
        Expression constant = propertyType == underlyingType ? raw : Expression.Convert(raw, propertyType);

        expression = operation switch
        {
            FilterOperation.Equals => Expression.Equal(property, constant),
            FilterOperation.NotEquals => Expression.NotEqual(property, constant),
            _ => null,
        };

        if (expression is not null)
            return true;

        error = $"Operation '{operation}' is not supported for guid properties.";
        return false;
    }
}
