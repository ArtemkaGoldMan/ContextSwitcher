using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// The app picker's list on Windows: the Start menu's apps, each with the icon the Start menu shows.
/// An icon is drawn once and kept as a PNG in the icon cache, as on the Mac, so that opening the picker
/// again is instant; one Windows won't draw leaves the app without, which the picker lives with.
///
/// Listing the Start menu and drawing every icon takes a few seconds the first time, so it is done as the
/// app starts (<see cref="IWarmUp"/>): the picker, and what asks which apps are open, find it ready.
/// </summary>
public sealed class WindowsInstalledApps : IInstalledAppsService, IWarmUp
{
    private readonly StartApps startApps;
    private readonly IAppIcons appIcons;
    private readonly ConfigPaths configPaths;
    private readonly InstalledAppsOptions options;
    private readonly ILogger logger;

    public WindowsInstalledApps(StartApps startApps, IAppIcons appIcons, ConfigPaths configPaths, InstalledAppsOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(startApps);
        ArgumentNullException.ThrowIfNull(appIcons);
        ArgumentNullException.ThrowIfNull(configPaths);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.startApps = startApps;
        this.appIcons = appIcons;
        this.configPaths = configPaths;
        this.options = options;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task WarmUpAsync(CancellationToken cancellationToken) =>
        _ = await this.GetInstalledAppsAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<StartApp> apps = await this.startApps.GetAsync(cancellationToken).ConfigureAwait(false);
        if (apps.Count == 0)
        {
            return [];
        }

        // Drawing blocks on Windows, so it is done off the caller's thread, one icon after another.
        return await Task.Run(() => this.WithIcons(apps, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The name of the file an app's icon is kept in: the app's name, without what a file name can't hold.</summary>
    public static string FileNameFor(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);

        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(appName.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim() + ".png";
    }

    private List<InstalledApp> WithIcons(IReadOnlyList<StartApp> apps, CancellationToken cancellationToken)
    {
        List<InstalledApp> installed = [];
        int notDrawn = 0;
        bool cacheUsable = this.TryCreateCache();
        foreach (StartApp app in apps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? icon = cacheUsable ? this.IconFor(app) : null;
            if (icon is null)
            {
                notDrawn++;
            }

            installed.Add(new InstalledApp(app.Name, icon));
        }

        if (notDrawn > 0)
        {
            this.logger.Note("App", "AppIconsNotDrawn", $"{notDrawn} of {apps.Count} apps in the Start menu are shown without an icon.");
        }

        return installed;
    }

    private string? IconFor(StartApp app)
    {
        string cached = Path.Combine(this.configPaths.IconCacheDirectory, FileNameFor(app.Name));
        if (File.Exists(cached))
        {
            return cached;
        }

        if (this.appIcons.RenderPng(app.AppId, this.options.IconSize) is not { } png)
        {
            return null;
        }

        // Written whole and then put in place, so that a picker opened meanwhile never reads half a PNG.
        string partial = $"{cached}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(partial, png);
            File.Move(partial, cached, overwrite: true);
            return cached;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.logger.Warn("App", "AppIconNotKept", $"The icon of {app.Name} couldn't be kept.", ex);
            TryDelete(partial);
            return null;
        }
    }

    private bool TryCreateCache()
    {
        try
        {
            Directory.CreateDirectory(this.configPaths.IconCacheDirectory);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.logger.Warn("App", "IconCacheNotMade", "The folder for the apps' icons couldn't be made; they are shown without.", ex);
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not logged: a leftover half file only takes space, and the line before this already said why.
        }
    }
}
