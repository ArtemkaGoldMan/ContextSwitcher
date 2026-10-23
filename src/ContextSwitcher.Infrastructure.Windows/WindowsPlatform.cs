using ContextSwitcher.Core.Platform;
using ContextSwitcher.Core.Security;
using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.Infrastructure.Windows;

/// <summary>
/// What Windows can do, what the app may run there, and which release files are Windows' - data the
/// Windows registrations choose. Each stage of <c>docs/windows-plan.md</c> turns on what it brings.
/// </summary>
public static class WindowsPlatform
{
    /// <summary>
    /// Stage 1: apps and time. No browser, music or Docker yet; no Focus, Apple Music, Dock or
    /// Automation permission at all - Windows has none of them.
    /// </summary>
    public static PlatformCapabilities Capabilities { get; } = new()
    {
        Focus = false,
        AppleMusic = false,
        Spotify = false,
        Docker = false,
        DockIcon = false,
        AutomationPermission = false,
        BrowserPages = false,
        Browsers = [],
        FocusFilterBrowsers = []
    };

    /// <summary>The commands the app runs on Windows.</summary>
    public static CommandAllowlist AllowedCommands { get; } = new(
    [
        // Opens a link in its app and shows a file in File Explorer (WindowsShell).
        "explorer"
    ]);

    /// <summary>
    /// The release files that are Windows': the list of releases Velopack's updater reads, and its
    /// installer. Velopack names them so for its default channel on Windows.
    /// </summary>
    public static ReleaseAssets Assets { get; } = new("releases.win.json", "-win-Setup.exe");
}
