using System.Reflection;
using Tiki.Shared.Querydsl.Attributes;

namespace Tiki.Shared.Querydsl.Utils;

internal static class PropertyUtils
{
    /// <summary>Case-insensitive property lookup, excluding anything marked <see cref="QuerydslIgnoreAttribute"/>.</summary>
    public static PropertyInfo? FindFilterableProperty(Type type, string propertyName) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p =>
                string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                p.GetCustomAttribute<QuerydslIgnoreAttribute>() is null);

    /// <summary>True if a property with this name exists but is explicitly excluded — used to give a precise validation message.</summary>
    public static bool IsIgnored(Type type, string propertyName) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p =>
                string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                p.GetCustomAttribute<QuerydslIgnoreAttribute>() is not null);

    public static bool UsesRawTextComparison(PropertyInfo property) =>
        property.GetCustomAttribute<UseRawTextComparisonAttribute>() is not null;
}
