using System.ComponentModel;
using System.Linq.Expressions;
using Tiki.Shared.Querydsl.Enums;

namespace Tiki.Shared.Querydsl.Utils;

/// <summary>Builds filter predicates for numeric properties (<c>byte</c>/<c>short</c>/<c>int</c>/<c>long</c>/<c>float</c>/<c>double</c>/<c>decimal</c>, nullable or not).</summary>
internal static class NumberExpressionUtils
{
    public static bool TryBuild(
        MemberExpression property, Type propertyType, FilterOperation operation, string? rawValue,
        out Expression? expression, out string? error)
    {
        error = null;
        expression = null;
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (operation is FilterOperation.IsNull or FilterOperation.IsNotNull)
            return TryBuildNullCheck(property, propertyType, operation, out expression, out error);

        if (operation == FilterOperation.In)
            return TryBuildIn(property, propertyType, underlyingType, rawValue, out expression, out error);

        if (!TryParse(underlyingType, rawValue, out var value))
        {
            error = $"Value '{rawValue}' could not be parsed as {underlyingType.Name}.";
            return false;
        }

        var constant = BuildConstant(value!, propertyType, underlyingType);

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

        error = $"Operation '{operation}' is not supported for numeric properties.";
        return false;
    }

    private static bool TryBuildNullCheck(
        MemberExpression property, Type propertyType, FilterOperation operation,
        out Expression? expression, out string? error)
    {
        expression = null;
        if (Nullable.GetUnderlyingType(propertyType) is null)
        {
            error = "IsNull/IsNotNull is not supported for a non-nullable numeric property.";
            return false;
        }

        error = null;
        var nullCheck = Expression.Equal(property, Expression.Constant(null, propertyType));
        expression = operation == FilterOperation.IsNull ? nullCheck : Expression.Not(nullCheck);
        return true;
    }

    private static bool TryBuildIn(
        MemberExpression property, Type propertyType, Type underlyingType, string? rawValue,
        out Expression? expression, out string? error)
    {
        expression = null;
        var rawValues = (rawValue ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsedValues = Array.CreateInstance(underlyingType, rawValues.Length);

        for (var i = 0; i < rawValues.Length; i++)
        {
            if (!TryParse(underlyingType, rawValues[i], out var parsed))
            {
                error = $"Value '{rawValues[i]}' could not be parsed as {underlyingType.Name}.";
                return false;
            }
            parsedValues.SetValue(parsed, i);
        }

        error = null;
        var containsMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
            .MakeGenericMethod(underlyingType);

        Expression valueExpr = propertyType == underlyingType ? property : Expression.Convert(property, underlyingType);
        expression = Expression.Call(containsMethod, Expression.Constant(parsedValues), valueExpr);
        return true;
    }

    private static Expression BuildConstant(object value, Type propertyType, Type underlyingType)
    {
        var raw = Expression.Constant(value, underlyingType);
        return propertyType == underlyingType ? raw : Expression.Convert(raw, propertyType);
    }

    private static bool TryParse(Type underlyingType, string? rawValue, out object? value)
    {
        value = null;
        if (rawValue is null)
            return false;

        try
        {
            var converter = TypeDescriptor.GetConverter(underlyingType);
            if (!converter.CanConvertFrom(typeof(string)))
                return false;

            value = converter.ConvertFromInvariantString(rawValue);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or NotSupportedException)
        {
            return false;
        }
    }
}
