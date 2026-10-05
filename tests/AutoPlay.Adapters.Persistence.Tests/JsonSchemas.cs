using System.Text.Json;
using Json.Schema;

namespace AutoPlay.Adapters.Persistence.Tests;

/// <summary>The published JSON schemas (docs/schemas), loaded once.</summary>
internal static class JsonSchemas
{
    private static readonly string Directory = Path.Combine(AppContext.BaseDirectory, "schemas");

    public static JsonSchema Profile { get; } = Load("profile");

    public static JsonSchema Screen { get; } = Load("screen");

    public static JsonSchema Sequence { get; } = Load("sequence");

    /// <summary>Returns the validation errors of a JSON document, or an empty list if it is valid.</summary>
    public static List<string> Validate(JsonSchema schema, string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid)
        {
            return [];
        }

        var errors = (result.Details ?? [])
            .Where(d => d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .ToList();
        return errors.Count > 0 ? errors : ["The document is not valid."];
    }

    private static JsonSchema Load(string name) => JsonSchema.FromFile(Path.Combine(Directory, $"{name}.schema.json"));
}
