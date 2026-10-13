namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Describes how a context manages browser startup behavior.
/// </summary>
public enum BrowserManagementMode
{
    /// <summary>
    /// Disables browser automation for the context.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("none")]
    None,

    /// <summary>
    /// Opens one or more configured URLs.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("urls")]
    Urls,

    /// <summary>
    /// Tab groups, from settings written before they were dropped: neither Safari nor Chrome lets
    /// another app open its tab groups (docs/browser-control.md). Kept only so such files still
    /// load - <see cref="BrowserManagementConfig"/> turns it into <see cref="Urls"/> or
    /// <see cref="None"/> as it is read - and never planned.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("groups")]
    Groups,

    /// <summary>
    /// Launches browser profiles with dedicated URLs.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("profiles")]
    Profiles
}
