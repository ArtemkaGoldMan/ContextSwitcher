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
    /// Builds a script listing the URL of every open tab, one per line, so a whole context's worth
    /// of URLs can be checked for duplicates with a single Apple Event instead of one tab sweep per
    /// URL (section 9.3). Measured on Chrome, three per-URL probes cost roughly 490ms against about
    /// 130ms for this, and this does not grow with the number of URLs.
    /// </summary>
    public static string ListTabUrls(string appName)
    {
        string escapedApp = EscapeStringLiteral(appName);
        return string.Join(
            '\n',
            $"tell application \"{escapedApp}\"",
            "    set collected to \"\"",
            "    repeat with w in windows",
            "        repeat with t in tabs of w",
            "            set collected to collected & (URL of t) & linefeed",
            "        end repeat",
            "    end repeat",
            "end tell",
            "return collected");
    }

    /// <summary>
    /// Separates a tab's URL from its title in <see cref="ListOpenTabs"/>' output. A unit separator
    /// rather than a tab character, which a page title can contain - and "tab" is a class in every
    /// browser's dictionary, so that constant cannot be named in the script anyway.
    /// </summary>
    public const char OpenTabFieldSeparator = (char)31;

    /// <summary>
    /// Builds a script listing every tab open in a browser as <c>url, separator, title</c> per line,
    /// for Profile Setup's "From open tabs" picker. Returns nothing when the browser is not running:
    /// scripting an app starts it, and opening a picker should not launch the user's browser.
    /// Safari calls a tab's title its <c>name</c>; the Chromium browsers call it <c>title</c>.
    /// </summary>
    public static string ListOpenTabs(string appName, bool isSafari)
    {
        string escapedApp = EscapeStringLiteral(appName);
        string titleProperty = isSafari ? "name" : "title";
        return string.Join(
            '\n',
            $"if application \"{escapedApp}\" is not running then return \"\"",
            $"set separator to character id {(int)OpenTabFieldSeparator}",
            "set collected to \"\"",
            $"tell application \"{escapedApp}\"",
            "    repeat with w in windows",
            "        try",
            "            repeat with t in tabs of w",
            $"                set collected to collected & (URL of t) & separator & ({titleProperty} of t) & linefeed",
            "            end repeat",
            "        end try",
            "    end repeat",
            "end tell",
            "return collected");
    }

    /// <summary>
    /// Builds a script listing Apple Music's playlists, one per line. Unless
    /// <paramref name="startMusicIfNeeded"/> is set it prints <see cref="MusicNotRunningMarker"/>
    /// instead of starting Music, since scripting an app launches it.
    /// </summary>
    public static string ListMusicPlaylists(bool startMusicIfNeeded)
    {
        List<string> lines = [];
        if (!startMusicIfNeeded)
        {
            lines.Add($"if application \"Music\" is not running then return \"{MusicNotRunningMarker}\"");
        }

        lines.AddRange(
        [
            "tell application \"Music\"",
            "    set playlistNames to name of every user playlist",
            "end tell",
            "set AppleScript's text item delimiters to linefeed",
            "return playlistNames as text"
        ]);

        return string.Join('\n', lines);
    }

    /// <summary>What <see cref="ListMusicPlaylists"/> prints when Music is not running.</summary>
    public const string MusicNotRunningMarker = "<music-not-running>";

    /// <summary>
    /// Builds a script that finds a Safari tab whose URL matches exactly and brings it to the
    /// front, for best-effort duplicate-tab avoidance (section 9.3). Returns <c>true</c>/<c>false</c>.
    /// Deliberately does not <c>activate</c> the browser: pulling the app to the foreground measured
    /// at about 2.0s of a switch that otherwise costs well under one, and selecting the tab and
    /// raising its window already leaves the right page waiting when the user goes to look.
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
    /// Does not <c>activate</c> the browser, for the reason given on <see cref="FocusSafariTabWithUrl"/>.
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

    /// <summary>
    /// Builds a script that plays a named Apple Music playlist (section 9.9).
    /// </summary>
    public static string PlayAppleMusicPlaylist(string playlist)
    {
        string escaped = EscapeStringLiteral(playlist);
        return $"tell application \"Music\" to play playlist \"{escaped}\"";
    }

    /// <summary>
    /// Builds a script that plays a Spotify URI, or best-effort attempts a plain playlist/track
    /// name (section 9.10). Spotify's scripting dictionary expects a URI for reliable playback;
    /// plain names are unsupported per the spec and may simply fail, which callers treat as a
    /// non-critical warning.
    /// </summary>
    public static string PlaySpotify(string uriOrName)
    {
        string escaped = EscapeStringLiteral(uriOrName);
        return string.Join(
            '\n',
            "tell application \"Spotify\"",
            "    activate",
            $"    play track \"{escaped}\"",
            "end tell");
    }
}
