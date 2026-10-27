using ContextSwitcher.Core.Platform;
using ContextSwitcher.Infrastructure.Windows;

namespace ContextSwitcher.Tests.Windows;

/// <summary>What Windows can do in stage 1, what it may run, and which release files are its own.</summary>
public sealed class WindowsPlatformTests
{
    /// <summary>Apps and time: the browser, music and Docker come in later stages, and Windows has no Focus, Apple Music, Dock or Automation permission.</summary>
    [Fact]
    public void Stage1HasAppsAlone()
    {
        PlatformCapabilities windows = WindowsPlatform.Capabilities;

        Assert.False(windows.Focus || windows.AppleMusic || windows.Spotify || windows.Docker || windows.DockIcon || windows.AutomationPermission || windows.BrowserPages);
        Assert.False(windows.PlaysMusic);
    }

    [Fact]
    public void FileExplorerAndPowerShellAreAllItRuns()
    {
        Assert.True(WindowsPlatform.AllowedCommands.IsAllowed("explorer"));
        Assert.True(WindowsPlatform.AllowedCommands.IsAllowed("powershell"));
        Assert.False(WindowsPlatform.AllowedCommands.IsAllowed("osascript"));
        Assert.False(WindowsPlatform.AllowedCommands.IsAllowed("cmd"));
    }

    /// <summary>A release with only the Mac's files is not Windows', so it is never offered here.</summary>
    [Fact]
    public void OnlyWindowsReleaseFilesAreItsOwn()
    {
        Assert.True(WindowsPlatform.Assets.Recognises("releases.win.json"));
        Assert.True(WindowsPlatform.Assets.Recognises("ContextSwitcher-win-Setup.exe"));
        Assert.False(WindowsPlatform.Assets.Recognises("ContextSwitcher.zip"));
        Assert.False(WindowsPlatform.Assets.Recognises("ContextSwitcher-0.7.0.dmg"));
    }
}
