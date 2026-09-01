namespace Tiki.Shared.Querydsl.Attributes;

/// <summary>Opts a string property out of Querydsl's default case-insensitive text comparison.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class UseRawTextComparisonAttribute : Attribute;
