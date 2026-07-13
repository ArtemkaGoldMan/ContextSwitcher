namespace ContextSwitcher.Infrastructure.AppleScript;

/// <summary>
/// Builds AppleScript source for the automation templates in agent.md section 9. All string
/// literals are escaped through <see cref="EscapeStringLiteral"/>, the single tested escaping
/// function required by section 13.
/// </summary>
public static class AppleScriptBuilder
{
    /// <summary>
    /// Escapes a value for safe interpolation inside an AppleScript double-quoted string literal.
    /// </summary>
    public static string EscapeStringLiteral(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Builds a script that gracefully quits the named application if it is running (section 9.1).
    /// </summary>
    public static string QuitApplicationIfRunning(string appName)
    {
        string escaped = EscapeStringLiteral(appName);
        return $"tell application \"{escaped}\" to if it is running then quit";
    }

    /// <summary>
    /// Builds a script that reports whether the named application is currently running (section 9.1).
    /// </summary>
    public static string IsApplicationRunning(string appName)
    {
        string escaped = EscapeStringLiteral(appName);
        return $"application \"{escaped}\" is running";
    }

    /// <summary>
    /// Builds a script that toggles macOS dark mode via System Events (section 9.6).
    /// </summary>
    public static string SetDarkMode(bool enabled)
    {
        string value = enabled ? "true" : "false";
        return $"tell application \"System Events\" to tell appearance preferences to set dark mode to {value}";
    }

    /// <summary>
    /// Builds a script that sets the desktop wallpaper via System Events (section 9.7).
    /// </summary>
    public static string SetWallpaper(string path, bool allSpaces)
    {
        string escaped = EscapeStringLiteral(path);
        string target = allSpaces ? "every desktop" : "desktop 1";
        return $"tell application \"System Events\" to tell {target} to set picture to \"{escaped}\"";
    }
}
