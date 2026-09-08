using System.Text.Json.Serialization;

namespace Tiki.Shared.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CitizenshipStatus
{
    Citizen,
    PermanentResident,
    Other,
}
