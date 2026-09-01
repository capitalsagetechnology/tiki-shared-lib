namespace Tiki.Shared.Core.Enums;

/// <summary>
/// ISO 3166-1 alpha-2 country codes for the markets Tiki operates in or plans to.
/// Universal, non-decision vocabulary only — this enum never carries business meaning
/// (eligibility, risk tier, routing) beyond "which country". Extend it as new markets
/// are added; it is never a place for a per-service subset.
/// </summary>
public enum CountryCode
{
    Unspecified = 0,
    NG,
    GH,
    KE,
    ZA,
    US,
    GB,
    CA,
    RW,
    UG,
    TZ,
    CI,
    SN,
    CM,
    EG,
    MA,
}
