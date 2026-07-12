namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Describes the supported theme modes that can be applied to a context.
/// </summary>
public enum ThemeMode
{
    /// <summary>
    /// Leaves the system theme unchanged.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("system")]
    System,

    /// <summary>
    /// Applies a light theme.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("light")]
    Light,

    /// <summary>
    /// Applies a dark theme.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("dark")]
    Dark
}
