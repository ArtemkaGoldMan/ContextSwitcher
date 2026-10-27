using System.Text;
using System.Text.Json;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// Reads what <see cref="StartApps.Script"/> prints: a JSON array of objects whose names and ids are
/// Base64 of UTF-8, so that no code page between PowerShell and the app can garble a name.
/// </summary>
public static class StartAppsParser
{
    /// <summary>
    /// The apps in <paramref name="output"/>, by name; an entry that doesn't read, or that
    /// <paramref name="filter"/> leaves out, is skipped. Of two apps with one name, the one with an
    /// AppUserModelID wins over one found by its path.
    /// </summary>
    /// <exception cref="JsonException">The output isn't JSON.</exception>
    public static IReadOnlyList<StartApp> Parse(string output, StartAppsFilter filter)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(filter);

        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        using JsonDocument document = JsonDocument.Parse(output);

        // PowerShell prints a single result as an object, not an array of one.
        IEnumerable<JsonElement> entries = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToList()
            : [document.RootElement.Clone()];

        List<StartApp> apps = [];
        foreach (JsonElement entry in entries)
        {
            string? name = Decode(entry, "n");
            string? id = Decode(entry, "i");
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
            {
                apps.Add(new StartApp(name.Trim(), id));
            }
        }

        return apps
            .Where(filter.Keeps)
            .OrderBy(app => StartAppsFilter.IsPath(app.AppId))
            .GroupBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string? Decode(JsonElement entry, string property)
    {
        if (entry.ValueKind != JsonValueKind.Object
            || !entry.TryGetProperty(property, out JsonElement value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value.GetString()!));
        }
        catch (FormatException)
        {
            // Not logged: one entry that isn't Base64 is skipped, and the rest of the list is still good.
            return null;
        }
    }
}
