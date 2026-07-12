namespace ContextSwitcher.Infrastructure.Files;

/// <summary>
/// Resolves the local ContextSwitcher paths under the user's configuration directory.
/// </summary>
public sealed class ConfigPaths
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigPaths"/> class.
    /// </summary>
    /// <param name="baseDirectory">An optional override for the config root.</param>
    public ConfigPaths(string? baseDirectory = null)
    {
        BaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "ContextSwitcher")
            : baseDirectory;
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
    /// Gets the path to the append-only analytics log.
    /// </summary>
    public string AnalyticsLogPath => Path.Combine(BaseDirectory, "analytics.jsonl");

    /// <summary>
    /// Gets the path to the append-only application log.
    /// </summary>
    public string AppLogPath => Path.Combine(BaseDirectory, "app.log.jsonl");

    /// <summary>
    /// Gets the directory used for configuration backups.
    /// </summary>
    public string BackupsDirectory => Path.Combine(BaseDirectory, "backups");

    /// <summary>
    /// Ensures the configuration root and backup directory exist before persistence operations.
    /// </summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }
}
