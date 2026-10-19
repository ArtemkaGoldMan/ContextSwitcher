namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// What each browser a profile can name is, wherever it runs - the one place "Chrome or Brave" is
/// written. Whether this computer has the browser is <see cref="Platform.PlatformCapabilities"/>'s to say.
/// </summary>
public static class KnownBrowsers
{
    /// <summary>
    /// Whether another app can open one of <paramref name="browser"/>'s profiles - a different
    /// signed-in person. Chrome's and Brave's can be; Safari's can't (docs/browser-control.md).
    /// </summary>
    public static bool HasProfiles(BrowserKind browser) => IsChromium(browser);

    /// <summary>
    /// Whether <paramref name="browser"/>'s tabs keep an id wherever they go, so a tab can be closed
    /// by it after its page moved to another address. Chrome's and Brave's do; Safari's have none.
    /// </summary>
    public static bool KeepsTabIds(BrowserKind browser) => IsChromium(browser);

    /// <summary>The name the app shows for <paramref name="browser"/>.</summary>
    public static string Name(BrowserKind browser) => browser switch
    {
        BrowserKind.Safari => "Safari",
        BrowserKind.Chrome => "Google Chrome",
        BrowserKind.Brave => "Brave",
        _ => throw new ArgumentOutOfRangeException(nameof(browser), browser, "The default browser has no name of its own.")
    };

    private static bool IsChromium(BrowserKind browser) => browser is BrowserKind.Chrome or BrowserKind.Brave;
}
