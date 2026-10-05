using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace AutoPlay.Adapters.Persistence;

internal static class JsonOptions
{
    public static JsonSerializerOptions Default { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { PersistOnlyRestorableProperties } },
    };

    /// <summary>
    /// Leaves out the properties that cannot be read back — computed ones such as <c>PixelRect.Right</c>
    /// or <c>NormalizedRect.Center</c>, which have no setter and no matching constructor parameter —
    /// so that the files only contain the actual data and match the JSON schemas.
    /// </summary>
    private static void PersistOnlyRestorableProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            var property = typeInfo.Properties[i];
            if (property.Set is null && property.AssociatedParameter is null)
            {
                typeInfo.Properties.RemoveAt(i);
            }
        }
    }
}
