namespace ContextSwitcher.Core.Configuration;

/// <summary>Changes to <see cref="AppConfiguration.IgnoredApps"/>, keeping the profiles in step with it.</summary>
public static class IgnoredApps
{
    /// <summary>
    /// <paramref name="configuration"/> with <paramref name="app"/> never touched: added to the list,
    /// and taken out of every profile that opens, closes or hides it. The profiles it was taken out of
    /// are returned too, to say so.
    /// </summary>
    public static (AppConfiguration Configuration, IReadOnlyList<string> TakenOutOf) Ignore(AppConfiguration configuration, string app)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(app);

        string name = app.Trim();
        List<string> takenOutOf = [];
        List<ContextDefinition> contexts = configuration.Contexts.Select(context =>
        {
            bool listed = context.LaunchApps.Concat(context.CloseApps).Concat(context.HideApps)
                .Contains(name, StringComparer.OrdinalIgnoreCase);
            if (!listed)
            {
                return context;
            }

            takenOutOf.Add(context.DisplayName);
            return context with
            {
                LaunchApps = Without(context.LaunchApps, name),
                CloseApps = Without(context.CloseApps, name),
                HideApps = Without(context.HideApps, name)
            };
        }).ToList();

        IReadOnlyList<string> ignored = configuration.IgnoredApps.Contains(name, StringComparer.OrdinalIgnoreCase)
            ? configuration.IgnoredApps
            : [.. configuration.IgnoredApps, name];
        return (configuration with { IgnoredApps = ignored, Contexts = contexts }, takenOutOf);
    }

    /// <summary><paramref name="configuration"/> with <paramref name="app"/> no longer ignored. No profile gets it back.</summary>
    public static AppConfiguration StopIgnoring(AppConfiguration configuration, string app)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration with { IgnoredApps = Without(configuration.IgnoredApps, app) };
    }

    private static IReadOnlyList<string> Without(IReadOnlyList<string> apps, string app) =>
        apps.Where(listed => !string.Equals(listed, app, StringComparison.OrdinalIgnoreCase)).ToList();
}
