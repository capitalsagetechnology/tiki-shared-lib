using System.Text.Json.Serialization;

namespace Tiki.Shared.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SanctionScreeningStatus
{
    None, // Sanction screening hasn't been done yet
    HasMatch, // Match found
    NoMatch // No match
}