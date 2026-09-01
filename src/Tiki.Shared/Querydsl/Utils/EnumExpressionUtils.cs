using System.Linq.Expressions;
using Tiki.Shared.Querydsl.Enums;

namespace Tiki.Shared.Querydsl.Utils;

/// <summary>Builds filter predicates for enum properties (nullable or not), parsing by member name — case-insensitive.</summary>
internal static class EnumExpressionUtils
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
                error = "IsNull/IsNotNull is not supported for a non-nullable enum property.";
                return false;
            }

            var nullCheck = Expression.Equal(property, Expression.Constant(null, propertyType));
            expression = operation == FilterOperation.IsNull ? nullCheck : Expression.Not(nullCheck);
            return true;
        }

        if (operation == FilterOperation.In)
        {
            var rawValues = (rawValue ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var parsedValues = Array.CreateInstance(underlyingType, rawValues.Length);
            for (var i = 0; i < rawValues.Length; i++)
            {
                if (!TryParseEnum(underlyingType, rawValues[i], out var parsed))
                {
                    error = $"Value '{rawValues[i]}' is not a valid {underlyingType.Name}.";
                    return false;
                }
                parsedValues.SetValue(parsed, i);
            }

            var containsMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                .MakeGenericMethod(underlyingType);

            Expression valueExpr = propertyType == underlyingType ? property : Expression.Convert(property, underlyingType);
            expression = Expression.Call(containsMethod, Expression.Constant(parsedValues), valueExpr);
            return true;
        }

        if (!TryParseEnum(underlyingType, rawValue, out var value))
        {
            error = $"Value '{rawValue}' is not a valid {underlyingType.Name}.";
            return false;
        }

        var raw = Expression.Constant(value, underlyingType);
        Expression constant = propertyType == underlyingType ? raw : Expression.Convert(raw, propertyType);

        expression = operation switch
        {
            FilterOperation.Equals => Expression.Equal(property, constant),
            FilterOperation.NotEquals => Expression.NotEqual(property, constant),
            _ => null,
        };

        if (expression is not null)
            return true;

        error = $"Operation '{operation}' is not supported for enum properties.";
        return false;
    }

    private static bool TryParseEnum(Type enumType, string? rawValue, out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        if (!Enum.TryParse(enumType, rawValue, ignoreCase: true, out var parsed))
            return false;

        if (!Enum.IsDefined(enumType, parsed))
            return false;

        value = parsed;
        return true;
    }
}
