using System.Text.Json.Serialization;

namespace Tiki.Shared.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AddressVerificationStatus
{
    Verified,
    Pending,
    Declined
}