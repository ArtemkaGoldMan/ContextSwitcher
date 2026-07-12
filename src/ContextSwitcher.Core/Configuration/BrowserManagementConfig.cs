using System.Text.Json.Serialization;

namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Configures how a context opens browser URLs, tab groups, or profiles.
/// </summary>
public sealed record BrowserManagementConfig
{
    /// <summary>
    /// Gets or sets the browser management mode for the context.
    /// </summary>
    public BrowserManagementMode Mode { get; init; } = BrowserManagementMode.None;

    /// <summary>
    /// Gets or sets the preferred browser for browser automation.
    /// </summary>
    public BrowserKind Browser { get; init; } = BrowserKind.Default;

    /// <summary>
    /// Gets or sets the startup URLs to open when the context is activated.
    /// </summary>
    public IReadOnlyList<string> Urls { get; init; } = [];

    /// <summary>
    /// Gets or sets the tab groups to activate when group mode is used.
    /// </summary>
    [JsonPropertyName("tab_groups")]
    public IReadOnlyList<string> TabGroups { get; init; } = [];

    /// <summary>
    /// Gets or sets browser profiles that should be launched for profile mode.
    /// </summary>
    public IReadOnlyList<BrowserProfileConfig> Profiles { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether duplicate tabs should be avoided when opening URLs.
    /// </summary>
    [JsonPropertyName("avoid_duplicate_tabs")]
    public bool AvoidDuplicateTabs { get; init; } = true;
}
