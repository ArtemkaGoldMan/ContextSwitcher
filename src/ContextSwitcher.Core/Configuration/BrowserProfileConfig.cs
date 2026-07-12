using System.Text.Json.Serialization;

namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Defines a browser profile target for profile-based browser automation.
/// </summary>
public sealed record BrowserProfileConfig
{
    /// <summary>
    /// Gets or sets the browser that should be launched with the profile.
    /// </summary>
    public BrowserKind Browser { get; init; } = BrowserKind.Chrome;

    /// <summary>
    /// Gets or sets the profile directory name for Chromium-based browsers.
    /// </summary>
    [JsonPropertyName("profile_directory")]
    public string ProfileDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the URLs that should be opened in the profile when activated.
    /// </summary>
    public IReadOnlyList<string> Urls { get; init; } = [];
}
