using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoPlay.Core.Storage;

internal static class JsonOptions
{
    public static JsonSerializerOptions Default { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
