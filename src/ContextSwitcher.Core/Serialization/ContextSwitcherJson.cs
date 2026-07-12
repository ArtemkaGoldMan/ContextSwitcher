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
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

        return options;
    }
}
