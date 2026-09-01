using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Serilog.Core;
using Serilog.Events;
using Tiki.Shared.Core.Attributes;

namespace Tiki.Shared.Logging;

/// <summary>
/// Finds every <see cref="SensitiveAttribute"/>-marked property on any type destructured
/// for structured logging (an <c>{@Object}</c> template argument, or a nested property of
/// one) and replaces its value with the masked form before the log event reaches any sink.
/// A type with no <see cref="SensitiveAttribute"/> property falls through to Serilog's
/// default destructuring, untouched.
/// </summary>
public sealed class SensitiveDataMaskingPolicy : IDestructuringPolicy
{
    private static readonly ConcurrentDictionary<Type, (PropertyInfo[] All, PropertyInfo[] Sensitive)> PropertyCache = new();

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        result = null;
        var type = value.GetType();

        // Scalars, strings, and collections are never structures carrying [Sensitive]
        // properties of their own — leave those to Serilog's default handling.
        if (type.IsPrimitive || value is string or IEnumerable)
            return false;

        var (allProperties, sensitiveProperties) = PropertyCache.GetOrAdd(type, BuildPropertyInfo);
        if (sensitiveProperties.Length == 0)
            return false;

        var members = new List<LogEventProperty>(allProperties.Length);
        foreach (var property in allProperties)
        {
            object? rawValue;
            try
            {
                rawValue = property.GetValue(value);
            }
            catch (TargetInvocationException)
            {
                continue; // a throwing getter should never break logging
            }

            var sensitive = property.GetCustomAttribute<SensitiveAttribute>();
            var propertyValue = sensitive is not null
                ? new ScalarValue(Mask(rawValue, sensitive.Strategy))
                : propertyValueFactory.CreatePropertyValue(rawValue, destructureObjects: true);

            members.Add(new LogEventProperty(property.Name, propertyValue));
        }

        result = new StructureValue(members, type.Name);
        return true;
    }

    private static (PropertyInfo[] All, PropertyInfo[] Sensitive) BuildPropertyInfo(Type type)
    {
        var all = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .ToArray();
        var sensitive = all.Where(p => p.GetCustomAttribute<SensitiveAttribute>() is not null).ToArray();
        return (all, sensitive);
    }

    private static string Mask(object? rawValue, SensitiveMaskStrategy strategy)
    {
        var text = rawValue?.ToString() ?? string.Empty;

        return strategy switch
        {
            SensitiveMaskStrategy.LastFourVisible => MaskLastFourVisible(text),
            SensitiveMaskStrategy.Hashed => Hash(text),
            _ => "***REDACTED***",
        };
    }

    private static string MaskLastFourVisible(string text) =>
        text.Length <= 4
            ? new string('*', text.Length)
            : new string('*', text.Length - 4) + text[^4..];

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
