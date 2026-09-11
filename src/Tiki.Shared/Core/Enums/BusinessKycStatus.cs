using System.Text.Json.Serialization;

namespace Tiki.Shared.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BusinessKycStatus
{
    NotStarted,
    Pending,
    InReview,
    Approved,
    ReVerify,
    Declined,
}
