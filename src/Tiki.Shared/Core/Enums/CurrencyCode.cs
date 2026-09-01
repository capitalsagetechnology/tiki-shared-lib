namespace Tiki.Shared.Core.Enums;

/// <summary>
/// ISO 4217 currency codes for the currencies Tiki settles, holds, or displays balances in.
/// Universal, non-decision vocabulary only — no FX policy, no settlement rules; those are
/// owned by whichever service decides them.
/// </summary>
public enum CurrencyCode
{
    Unspecified = 0,
    NGN,
    GHS,
    KES,
    ZAR,
    USD,
    GBP,
    EUR,
    CAD,
    RWF,
    UGX,
    TZS,
    XOF,
    XAF,
    EGP,
    MAD,
}
