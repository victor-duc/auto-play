using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPlay.Adapters.Persistence;

/// <summary>
/// Versioned JSON format of the stored files. Every file starts with a <c>schemaVersion</c> property;
/// the JSON schemas of the current version are in <c>docs/schemas</c>.
/// </summary>
/// <remarks>
/// Versions:
/// <list type="bullet">
/// <item>0 — files written before versioning: no <c>schemaVersion</c>, and computed geometry
/// properties (<c>right</c>, <c>center</c>, <c>isEmpty</c>…) that are ignored on read.</item>
/// <item>1 — <c>schemaVersion</c> added, computed properties removed.</item>
/// </list>
/// When the format changes, increment <see cref="CurrentVersion"/>, update the schemas and add the
/// conversion of older files to <see cref="Migrate"/>.
/// </remarks>
internal static class FileFormat
{
    public const int CurrentVersion = 1;

    public const string VersionProperty = "schemaVersion";

    public static string Serialize<T>(T value)
    {
        if (JsonSerializer.SerializeToNode(value, JsonOptions.Default) is not JsonObject content)
        {
            throw new InvalidOperationException($"{typeof(T).Name} must serialize to a JSON object.");
        }

        // The version comes first so that it is visible at a glance.
        var document = new JsonObject { [VersionProperty] = CurrentVersion };
        foreach (var (name, node) in content.ToList())
        {
            content.Remove(name);
            document[name] = node;
        }

        return document.ToJsonString(JsonOptions.Default);
    }

    /// <exception cref="InvalidDataException">The file is not valid, or was written by a newer version.</exception>
    public static T Deserialize<T>(string json, string path)
    {
        if (JsonNode.Parse(json) is not JsonObject document)
        {
            throw new InvalidDataException($"The file '{path}' does not contain a JSON object.");
        }

        var version = ReadVersion(document, path);
        if (version > CurrentVersion)
        {
            throw new InvalidDataException(
                $"The file '{path}' was written by a newer version of AutoPlay (format {version}; this version reads up to {CurrentVersion}).");
        }

        Migrate(document, version);
        document.Remove(VersionProperty);
        return document.Deserialize<T>(JsonOptions.Default)
            ?? throw new InvalidDataException($"The file '{path}' is empty.");
    }

    private static int ReadVersion(JsonObject document, string path)
    {
        if (!document.TryGetPropertyValue(VersionProperty, out var node))
        {
            return 0;
        }

        if (node is JsonValue value && value.TryGetValue<int>(out var version) && version >= 0)
        {
            return version;
        }

        throw new InvalidDataException($"The file '{path}' has an invalid {VersionProperty}.");
    }

    /// <summary>Converts a document of an older version to the current one, in place.</summary>
    private static void Migrate(JsonObject document, int version)
    {
        // 0 → 1: nothing to convert; the computed properties of version 0 are ignored on read.
        _ = document;
        _ = version;
    }
}
