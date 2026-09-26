using System.ComponentModel.DataAnnotations;

namespace Tiki.Shared.Auth.ApiKeys;

/// <summary>Configuration shared by the writer (Identity) and the reader (the gateway).</summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "Tiki:ApiKeys";

    /// <summary>
    /// Server-side secret mixed into every key hash. Keys Tiki mints carry 256 bits of entropy
    /// and would be safe under a bare SHA-256, but keys imported from the legacy platform may
    /// not — with a pepper, a copy of the hash column alone cannot be brute-forced. Identity and
    /// the gateway must hold the same value; changing it invalidates every key.
    /// </summary>
    [Required, MinLength(32)]
    public string Pepper { get; set; } = "";
}
