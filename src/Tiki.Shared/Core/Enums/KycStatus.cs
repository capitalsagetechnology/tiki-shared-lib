using System.Text.Json.Serialization;

namespace Tiki.Shared.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KycStatus
{
    NotStarted,
    Pending,
    InReview,
    Approved,
    ReVerify,
    Declined,
}
