namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// The <c>apps</c> map of the settings: which apps the profiles name, which have an entry, and adding the
/// entries that are missing. An entry already there is never replaced - a person may have edited it by hand.
/// </summary>
public static class AppsMap
{
    /// <summary>
    /// The apps the profiles open, close or hide, each once whatever its case, in the order met; not
    /// those the person never wants touched.
    /// </summary>
    public static IReadOnlyList<string> NamedIn(AppConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        List<string> names = [];
        foreach (ContextDefinition context in configuration.Contexts)
        {
            foreach (string name in context.LaunchApps.Concat(context.CloseApps).Concat(context.HideApps))
            {
                if (!string.IsNullOrWhiteSpace(name)
                    && !names.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase)
                    && !configuration.IgnoredApps.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    names.Add(name.Trim());
                }
            }
        }

        return names;
    }

    /// <summary>The entry for <paramref name="appName"/>, whatever the case; null when there is none.</summary>
    public static AppIdentity? Of(AppConfiguration configuration, string appName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(appName);

        return configuration.Apps is { } apps
            ? apps.FirstOrDefault(entry => string.Equals(entry.Key, appName.Trim(), StringComparison.OrdinalIgnoreCase)).Value
            : null;
    }

    /// <summary>The apps the profiles name that the map has no entry for.</summary>
    public static IReadOnlyList<string> Unrecorded(AppConfiguration configuration) =>
        NamedIn(configuration).Where(name => Of(configuration, name) is null).ToList();

    /// <summary>
    /// <paramref name="configuration"/> with <paramref name="found"/> added to the map, for the apps the
    /// profiles still name and the map has no entry for; null when that leaves it as it was.
    /// </summary>
    public static AppConfiguration? With(AppConfiguration configuration, IReadOnlyDictionary<string, AppIdentity> found)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(found);

        Dictionary<string, AppIdentity> apps = new(configuration.Apps ?? new Dictionary<string, AppIdentity>(), StringComparer.Ordinal);
        bool added = false;
        foreach (string name in Unrecorded(configuration))
        {
            if (found.FirstOrDefault(entry => string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase)) is { Key: not null } match)
            {
                apps[name] = match.Value;
                added = true;
            }
        }

        return added ? configuration with { Apps = apps } : null;
    }
}
