namespace ContextSwitcher.Infrastructure.Files;

/// <summary>
/// Resolves the local ContextSwitcher paths under the user's configuration directory.
/// </summary>
public sealed class ConfigPaths
{
    /// <summary>
    /// An environment variable naming the folder to keep everything in, instead of the usual one -
    /// for another copy alongside, or a test.
    /// </summary>
    public const string DirectoryVariable = "CONTEXTSWITCHER_CONFIG_DIR";

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigPaths"/> class.
    /// </summary>
    /// <param name="baseDirectory">An optional override for the config root.</param>
    public ConfigPaths(string? baseDirectory = null)
    {
        BaseDirectory = ResolveBaseDirectory(
            baseDirectory,
            Environment.GetEnvironmentVariable(DirectoryVariable),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            AppBuild.IsDevelopment,
            OperatingSystem.IsWindows() ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) : null);
    }

    /// <summary>
    /// Where everything is kept: the folder given, else the one <see cref="DirectoryVariable"/> names,
    /// else <c>~/.config/ContextSwitcher</c> - or <c>~/.config/ContextSwitcher-dev</c> for a
    /// development build, which so never reads or changes the installed app's profiles and time.
    /// On Windows, where <paramref name="windowsAppData"/> is <c>%APPDATA%</c>, the folder is in there
    /// instead: <c>%APPDATA%\ContextSwitcher</c>, where Windows apps keep their settings. Not
    /// <c>%LOCALAPPDATA%</c>, where the installer puts the app and which it replaces on each update.
    /// </summary>
    public static string ResolveBaseDirectory(
        string? explicitDirectory, string? environmentDirectory, string userHome, bool isDevelopment, string? windowsAppData = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitDirectory))
        {
            return explicitDirectory;
        }

        if (!string.IsNullOrWhiteSpace(environmentDirectory))
        {
            // A quoted "~/…" reaches us unexpanded.
            string directory = environmentDirectory.Trim();
            return directory == "~" || directory.StartsWith("~/", StringComparison.Ordinal)
                ? Path.Combine(userHome, directory.TrimStart('~').TrimStart('/'))
                : directory;
        }

        string name = isDevelopment ? "ContextSwitcher-dev" : "ContextSwitcher";
        return string.IsNullOrWhiteSpace(windowsAppData)
            ? Path.Combine(userHome, ".config", name)
            : Path.Combine(windowsAppData, name);
    }

    /// <summary>
    /// Gets the resolved root directory used for local configuration and state files.
    /// </summary>
    public string BaseDirectory { get; }

    /// <summary>
    /// Gets the path to the persisted settings JSON file.
    /// </summary>
    public string SettingsPath => Path.Combine(BaseDirectory, "settings.json");

    /// <summary>
    /// Gets the path to the persisted state JSON file.
    /// </summary>
    public string StatePath => Path.Combine(BaseDirectory, "state.json");

    /// <summary>
    /// Gets the path to the tabs each profile opened in Chrome or Brave for pages it closes on leaving.
    /// </summary>
    public string OpenedTabsPath => Path.Combine(BaseDirectory, "opened-tabs.json");

    /// <summary>
    /// Gets the path to the append-only analytics log.
    /// </summary>
    public string AnalyticsLogPath => Path.Combine(BaseDirectory, "analytics.jsonl");

    /// <summary>
    /// Gets the path to the marker for the currently in-progress analytics session, used to detect
    /// and recover a session left open by a crash (section 6.3). Absent during normal operation
    /// except while a session is actively open; deleted as soon as that session ends cleanly.
    /// </summary>
    public string AnalyticsMarkerPath => Path.Combine(BaseDirectory, "analytics.current.json");

    /// <summary>When the app last said it was running; see <c>AnalyticsService.RecordAliveAsync</c>.</summary>
    public string AnalyticsAlivePath => Path.Combine(BaseDirectory, "analytics.alive");

    /// <summary>
    /// Gets the path to the append-only application log.
    /// </summary>
    public string AppLogPath => Path.Combine(BaseDirectory, "app.log.jsonl");

    /// <summary>
    /// Gets the directory used for configuration backups.
    /// </summary>
    public string BackupsDirectory => Path.Combine(BaseDirectory, "backups");

    /// <summary>
    /// Gets the directory holding PNG icons extracted from installed application bundles for the
    /// app picker. Purely a derived cache - safe to delete, rebuilt on demand.
    /// </summary>
    public string IconCacheDirectory => Path.Combine(BaseDirectory, "icon-cache");

    /// <summary>
    /// Ensures the configuration root and backup directory exist before persistence operations.
    /// </summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }
}
