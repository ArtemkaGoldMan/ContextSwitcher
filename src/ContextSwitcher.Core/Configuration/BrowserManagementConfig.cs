using System.Text.Json.Serialization;

namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Configures how a context opens browser URLs or profiles.
/// </summary>
public sealed record BrowserManagementConfig : IJsonOnDeserialized
{
    private BrowserManagementMode mode = BrowserManagementMode.None;

    /// <summary>
    /// Gets or sets the browser management mode for the context.
    /// </summary>
    public BrowserManagementMode Mode
    {
        get => this.mode;
        init => this.mode = value;
    }

    /// <summary>
    /// Gets or sets the preferred browser for browser automation.
    /// </summary>
    public BrowserKind Browser { get; init; } = BrowserKind.Default;

    /// <summary>
    /// Gets or sets the startup URLs to open when the context is activated.
    /// </summary>
    public IReadOnlyList<string> Urls { get; init; } = [];

    /// <summary>
    /// Pages whose tabs close when the context is left, in <see cref="BrowserManagementMode.Urls"/>
    /// mode with a named browser. A page here may also be in <see cref="Urls"/> - opened on the way
    /// in, closed on the way out - or only here. A tab matches when it shows the page or anything
    /// under it (<c>PageMatch</c>); every other tab is left alone.
    /// </summary>
    [JsonPropertyName("close_urls")]
    public IReadOnlyList<string> CloseUrls { get; init; } = [];

    /// <summary>
    /// Gets or sets browser profiles that should be launched for profile mode.
    /// </summary>
    public IReadOnlyList<BrowserProfileConfig> Profiles { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether duplicate tabs should be avoided when opening URLs.
    /// </summary>
    [JsonPropertyName("avoid_duplicate_tabs")]
    public bool AvoidDuplicateTabs { get; init; } = true;

    /// <summary>
    /// A profile saved in tab groups mode never opened a group - no browser lets another app do
    /// that - only the pages it fell back to, if it had any. It is read as what it did: a pages
    /// profile with those pages, or one that opens nothing. Its <c>tab_groups</c> names are no
    /// longer a setting, so they are skipped on the way in and gone on the next save.
    /// </summary>
    void IJsonOnDeserialized.OnDeserialized()
    {
        if (this.mode == BrowserManagementMode.Groups)
        {
            this.mode = this.Urls.Count > 0 || this.CloseUrls.Count > 0
                ? BrowserManagementMode.Urls
                : BrowserManagementMode.None;
        }
    }
}
