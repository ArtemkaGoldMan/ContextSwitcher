namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Identifies the supported browsers that can be targeted by automation.
/// </summary>
public enum BrowserKind
{
    /// <summary>
    /// Uses the system default browser.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("Default")]
    Default,

    /// <summary>
    /// Targets Google Chrome.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("Chrome")]
    Chrome,

    /// <summary>
    /// Targets Brave Browser.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("Brave")]
    Brave,

    /// <summary>
    /// Targets Safari.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("Safari")]
    Safari
}
