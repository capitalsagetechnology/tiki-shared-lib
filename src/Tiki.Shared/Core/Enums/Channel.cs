using System.Text.Json.Serialization;

namespace Tiki.Shared.Core.Enums;

/// <summary>
/// The surface a request originated from. Universal and non-decision — services may
/// branch presentation or rate limits on it, but it never encodes a business outcome.
/// </summary>
/// 
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Channel
{
    Unspecified = 0,
    Web,
    MobileApp,
    Ussd,
    Api,
    Agent,
    InternalService,
}
