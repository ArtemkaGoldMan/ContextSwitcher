namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Represents a quick link that can be shown for a context in the dashboard.
/// </summary>
public sealed record QuickLinkConfig
{
    /// <summary>
    /// Gets or sets the display title of the quick link.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL that the quick link opens.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the icon identifier used to render the quick link.
    /// </summary>
    public string Icon { get; init; } = "link";
}
