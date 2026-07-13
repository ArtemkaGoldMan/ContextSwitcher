using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContextSwitcher.Core.Serialization;

/// <summary>
/// Centralizes the JSON serialization configuration used by the core project.
/// </summary>
public static class ContextSwitcherJson
{
    /// <summary>
    /// Gets the shared serializer options for ContextSwitcher JSON documents.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions(writeIndented: true);

    /// <summary>
    /// Gets serializer options for single-line JSON, used for append-only <c>.jsonl</c> files.
    /// </summary>
    public static JsonSerializerOptions CompactOptions { get; } = CreateOptions(writeIndented: false);

    private static JsonSerializerOptions CreateOptions(bool writeIndented)
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

        return options;
    }
}
