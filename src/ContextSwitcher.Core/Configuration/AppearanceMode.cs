namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Whether the app's windows are light, dark, or follow macOS.
/// </summary>
public enum AppearanceMode
{
    /// <summary>
    /// Follows the light or dark appearance chosen in macOS, and changes with it.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("system")]
    System,

    /// <summary>
    /// Always light.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("light")]
    Light,

    /// <summary>
    /// Always dark.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("dark")]
    Dark
}
