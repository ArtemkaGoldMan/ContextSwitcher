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
    /// Activates configured browser tab groups.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("groups")]
    Groups,

    /// <summary>
    /// Launches browser profiles with dedicated URLs.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("profiles")]
    Profiles
}
