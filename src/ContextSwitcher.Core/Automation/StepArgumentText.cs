using System.Globalization;
using System.Text.Json;
using ContextSwitcher.Core.Serialization;

namespace ContextSwitcher.Core.Automation;

/// <summary>How a step's arguments are written as text, and read back.</summary>
internal static class StepArgumentText
{
    /// <summary>
    /// A list, as JSON. Lists used to be joined with commas, which split any address that held one -
    /// a map's coordinates, a query's values - into pieces that opened nothing.
    /// </summary>
    public static string List<T>(IReadOnlyList<T> values) => JsonSerializer.Serialize(values, ContextSwitcherJson.CompactOptions);

    /// <summary>The list under <paramref name="key"/>; empty when there is none.</summary>
    public static IReadOnlyList<T> List<T>(IReadOnlyDictionary<string, string> arguments, string key) =>
        arguments.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? JsonSerializer.Deserialize<IReadOnlyList<T>>(value, ContextSwitcherJson.CompactOptions) ?? []
            : [];

    public static string Flag(bool value) => value.ToString(CultureInfo.InvariantCulture);

    public static bool Flag(IReadOnlyDictionary<string, string> arguments, string key) =>
        arguments.GetValueOrDefault(key) == Flag(true);

    public static string Text(IReadOnlyDictionary<string, string> arguments, string key) =>
        arguments.GetValueOrDefault(key, string.Empty);

    public static TEnum Choice<TEnum>(IReadOnlyDictionary<string, string> arguments, string key, TEnum otherwise)
        where TEnum : struct, Enum =>
        arguments.TryGetValue(key, out string? value) ? Enum.Parse<TEnum>(value) : otherwise;
}
