namespace Tiki.Shared.Querydsl.Attributes;

/// <summary>
/// Excludes a property from ever being filterable/sortable. A filter or sort request
/// naming it fails validation with a structured error — never a silent no-op, never an
/// exception leaking internal type detail.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class QuerydslIgnoreAttribute : Attribute;
