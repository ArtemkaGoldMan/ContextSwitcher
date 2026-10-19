using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Platform;
using ContextSwitcher.Core.Security;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>What a Mac can do, and what the app may run on one - data the Mac's registrations choose.</summary>
public static class MacPlatform
{
    /// <summary>Everything the app does today.</summary>
    public static PlatformCapabilities Capabilities { get; } = new()
    {
        Focus = true,
        AppleMusic = true,
        DockIcon = true,
        Browsers = [BrowserKind.Safari, BrowserKind.Chrome, BrowserKind.Brave],
        FocusFilterBrowsers = [BrowserKind.Safari]
    };

    /// <summary>The commands the app runs on a Mac.</summary>
    public static CommandAllowlist AllowedCommands { get; } = new(
    [
        "osascript",
        "open",
        "docker",
        "shortcuts",

        // Built-in macOS image converter, used only to transcode an installed app's .icns bundle
        // icon into a cached PNG for the Profile Setup app picker. Read-only with respect to the
        // app bundle: it writes solely into ConfigPaths.IconCacheDirectory.
        "sips",

        // The updater (AppBundleUpdater): ditto unpacks a downloaded release, plutil reads its
        // version, and codesign checks it was signed with the same certificate as this copy.
        "ditto",
        "plutil",
        "codesign"
    ]);
}
