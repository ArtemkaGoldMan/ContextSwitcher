using System.Text.Json;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Serialization;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>Reads what the plan builder wrote into a step's arguments.</summary>
internal static class StepArguments
{
    /// <summary>A list argument, carried as JSON by the plan builder.</summary>
    public static string[] List(IReadOnlyDictionary<string, string> arguments, string key)
    {
        return arguments.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? JsonSerializer.Deserialize<string[]>(value, ContextSwitcherJson.CompactOptions) ?? []
            : [];
    }

    /// <summary>
    /// A list of addresses, carried as JSON: an address can hold the comma the other lists are
    /// joined with.
    /// </summary>
    public static IReadOnlyList<string> Strings(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<string>>(json, ContextSwitcherJson.CompactOptions) ?? [];

    public static IReadOnlyList<BrowserProfileConfig> Profiles(string profilesJson)
    {
        return JsonSerializer.Deserialize<IReadOnlyList<BrowserProfileConfig>>(profilesJson, ContextSwitcherJson.CompactOptions) ?? [];
    }
}
