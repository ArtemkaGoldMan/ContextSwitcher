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

    /// <summary>
    /// Builds a script that finds a Safari tab whose URL matches exactly and brings it to the
    /// front, for best-effort duplicate-tab avoidance (section 9.3). Returns <c>true</c>/<c>false</c>.
    /// </summary>
    public static string FocusSafariTabWithUrl(string url)
    {
        string escapedUrl = EscapeStringLiteral(url);
        return string.Join(
            '\n',
            "tell application \"Safari\"",
            "    repeat with w in windows",
            "        repeat with t in tabs of w",
            $"            if URL of t is \"{escapedUrl}\" then",
            "                set current tab of w to t",
            "                set index of w to 1",
            "                activate",
            "                return true",
            "            end if",
            "        end repeat",
            "    end repeat",
            "end tell",
            "return false");
    }

    /// <summary>
    /// Builds a script that finds a tab whose URL matches exactly in a Chromium-based browser
    /// (Chrome or Brave) and brings it to the front (section 9.3). Returns <c>true</c>/<c>false</c>.
    /// </summary>
    public static string FocusChromiumTabWithUrl(string appName, string url)
    {
        string escapedApp = EscapeStringLiteral(appName);
        string escapedUrl = EscapeStringLiteral(url);
        return string.Join(
            '\n',
            $"tell application \"{escapedApp}\"",
            "    repeat with w in windows",
            "        set tabIndex to 0",
            "        repeat with t in tabs of w",
            "            set tabIndex to tabIndex + 1",
            $"            if URL of t is \"{escapedUrl}\" then",
            "                set active tab index of w to tabIndex",
            "                set index of w to 1",
            "                activate",
            "                return true",
            "            end if",
            "        end repeat",
            "    end repeat",
            "end tell",
            "return false");
    }

    /// <summary>
    /// Builds a best-effort script that activates a named Safari tab group (section 9.4). Tab
    /// group scripting support varies by macOS version and is not guaranteed to exist, so this is
    /// wrapped in <c>try/on error</c> and reports <c>false</c> rather than propagating a script error.
    /// </summary>
    public static string ActivateSafariTabGroup(string groupName)
    {
        string escaped = EscapeStringLiteral(groupName);
        return string.Join(
            '\n',
            "tell application \"Safari\"",
            "    try",
            $"        set targetGroup to tab group \"{escaped}\" of window 1",
            "        set current tab group of window 1 to targetGroup",
            "        activate",
            "        return true",
            "    on error",
            "        return false",
            "    end try",
            "end tell");
    }

    /// <summary>
    /// Builds a best-effort script that activates a named tab group in a Chromium-based browser
    /// (section 9.4). Chrome's scripting dictionary does not reliably expose tab groups, so this
    /// is wrapped in <c>try/on error</c> and reports <c>false</c> rather than propagating a script error.
    /// </summary>
    public static string ActivateChromiumTabGroup(string appName, string groupName)
    {
        string escapedApp = EscapeStringLiteral(appName);
        string escapedGroup = EscapeStringLiteral(groupName);
        return string.Join(
            '\n',
            $"tell application \"{escapedApp}\"",
            "    try",
            $"        set targetGroup to tab group \"{escapedGroup}\" of window 1",
            "        activate",
            "        return true",
            "    on error",
            "        return false",
            "    end try",
            "end tell");
    }
}
