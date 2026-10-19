using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>The Mac apps of the browsers a profile can name: the one place their names are written.</summary>
public static class MacBrowserApps
{
    /// <summary>
    /// The name of <paramref name="browser"/>'s app in Applications, which AppleScript, <c>open</c>
    /// and the browser's main process all go by. The default browser has none: it is whichever the
    /// person chose.
    /// </summary>
    public static string Name(BrowserKind browser) => browser switch
    {
        BrowserKind.Chrome => "Google Chrome",
        BrowserKind.Brave => "Brave Browser",
        BrowserKind.Safari => "Safari",
        _ => throw new ArgumentOutOfRangeException(nameof(browser), browser, "Default browser has no fixed application name.")
    };
}
