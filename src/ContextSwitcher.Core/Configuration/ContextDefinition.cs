using System.Text.Json.Serialization;

namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Represents a named context that can be switched to with its own automation settings.
/// </summary>
public sealed record ContextDefinition
{
    /// <summary>
    /// Gets or sets the stable identifier for the context.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable display name shown in the UI.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the short label used in the menu bar.
    /// </summary>
    public string MenuBarLabel { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the accent color used to identify the context.
    /// </summary>
    public string AccentColor { get; init; } = "#2F6FED";

    /// <summary>
    /// Gets or sets the icon identifier for the context.
    /// </summary>
    public string Icon { get; init; } = "circle";

    /// <summary>
    /// Gets or sets the applications that should be closed when leaving this context.
    /// </summary>
    public IReadOnlyList<string> CloseApps { get; init; } = [];

    /// <summary>
    /// Gets or sets the applications that should be launched when entering this context.
    /// </summary>
    public IReadOnlyList<string> LaunchApps { get; init; } = [];

    /// <summary>
    /// Gets or sets the browser automation configuration for the context.
    /// </summary>
    [JsonPropertyName("browser_management")]
    public BrowserManagementConfig BrowserManagement { get; init; } = new();

    /// <summary>
    /// Gets or sets the theme configuration for the context.
    /// </summary>
    public ThemeConfig Theme { get; init; } = new();

    /// <summary>
    /// Gets or sets the wallpaper configuration for the context.
    /// </summary>
    public WallpaperConfig Wallpaper { get; init; } = new();

    /// <summary>
    /// Gets or sets the focus mode configuration for the context.
    /// </summary>
    public FocusConfig Focus { get; init; } = new();

    /// <summary>
    /// Gets or sets the media configuration for the context.
    /// </summary>
    public MediaConfig Media { get; init; } = new();

    /// <summary>
    /// Gets or sets the Docker resources that should be managed for the context.
    /// </summary>
    public DockerResourceConfig Docker { get; init; } = new();

    /// <summary>
    /// Gets or sets the quick links associated with the context.
    /// </summary>
    public IReadOnlyList<QuickLinkConfig> QuickLinks { get; init; } = [];

    /// <summary>
    /// Gets or sets the contextual notes shown in the dashboard.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>
    /// Gets or sets the switch policy for handling automation failures.
    /// </summary>
    public SwitchPolicyConfig SwitchPolicy { get; init; } = new();
}
