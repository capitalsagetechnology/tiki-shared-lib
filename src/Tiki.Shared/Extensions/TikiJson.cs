using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tiki.Shared.Extensions;

/// <summary>
/// Shared <see cref="System.Text.Json"/> defaults — camelCase property names, enums as
/// strings — used consistently by ASP.NET Core, the Kafka producer/consumer, and gRPC JSON
/// transcoding where applicable. A payload serialized in one service and deserialized in
/// another round-trips without custom converters on either side.
/// </summary>
public static class TikiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = false,
    };
}
