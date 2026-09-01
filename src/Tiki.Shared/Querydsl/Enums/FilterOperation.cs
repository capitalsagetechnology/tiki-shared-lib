namespace Tiki.Shared.Querydsl.Enums;

public enum FilterOperation
{
    Equals = 0,
    NotEquals,
    Contains,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    In,
    IsNull,
    IsNotNull,
}
