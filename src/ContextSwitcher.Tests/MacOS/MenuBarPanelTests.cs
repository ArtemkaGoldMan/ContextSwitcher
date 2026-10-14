using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

/// <summary>
/// The popover is turned into a panel only in the running app, which has windows; what a test can
/// check is that without one nothing is touched and it says so, so the caller shows an ordinary
/// window instead. Measured in the running app with the app in the background, before and after:
/// the app came to the front (and macOS left a full-screen app for a desktop); now it stays behind
/// and the popover opens on the desktop in front.
/// </summary>
public sealed class MenuBarPanelTests
{
    [Fact]
    public void WithoutAWindowNothingIsMadeAPanel()
    {
        Assert.False(MenuBarPanel.Make(IntPtr.Zero));
        Assert.Null(MenuBarPanel.Describe(IntPtr.Zero));
    }

    [Fact]
    public void ThePanelStyleIsAppKitsNonactivatingOne()
    {
        Assert.Equal((nuint)128, MenuBarPanel.NonactivatingPanelStyle);
    }
}
