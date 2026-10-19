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
    /// <param name="settingsRoot">
    /// The folder this system keeps apps' settings in - the platform's <see cref="SettingsRoot"/> - or
    /// none for <c>~/.config</c>.
    /// </param>
    /// <param name="development">
    /// Whether this is a development build, which keeps a folder of its own: the app's composition root
    /// says, from the app's identity.
    /// </param>
    public ConfigPaths(string? baseDirectory = null, string? settingsRoot = null, bool development = false)
    {
        BaseDirectory = ResolveBaseDirectory(
            baseDirectory,
            Environment.GetEnvironmentVariable(DirectoryVariable),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            development,
            settingsRoot);
    }

    /// <summary>
    /// Where everything is kept: the folder given, else the one <see cref="DirectoryVariable"/> names,
    /// else <c>ContextSwitcher</c> in <paramref name="settingsRoot"/> - <c>~/.config</c> unless another
    /// is given - or <c>ContextSwitcher-dev</c> for a development build, which so never reads or
    /// changes the installed app's profiles and time.
    /// </summary>
    public static string ResolveBaseDirectory(
        string? explicitDirectory, string? environmentDirectory, string userHome, bool isDevelopment, string? settingsRoot = null)
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
        return string.IsNullOrWhiteSpace(settingsRoot)
            ? Path.Combine(userHome, ".config", name)
            : Path.Combine(settingsRoot, name);
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

    /// <summary>The file held while a switch runs, so no other process switches at the same time.</summary>
    public string SwitchLockPath => Path.Combine(BaseDirectory, "state.json.lock");

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
