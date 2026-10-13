using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

/// <summary>
/// The menu bar popover opened on the desktop it was first shown on, not the one the user was
/// looking at, so "Open Dashboard" seemed to do nothing. Measured in the running app before the fix:
/// collectionBehavior 0x80, onActiveSpace false; after: 0x102, onActiveSpace true.
/// </summary>
public sealed class WindowSpacesTests
{
    /// <summary>What every Avalonia window starts with - able to go full screen - becomes following the user.</summary>
    [Fact]
    public void AnAvaloniaWindowFollowsTheActiveSpaceAndShowsOverFullScreenApps()
    {
        Assert.Equal(WindowSpaces.MoveToActiveSpace | WindowSpaces.FullScreenAuxiliary, WindowSpaces.FollowingTheActiveSpace(WindowSpaces.FullScreenPrimary));
        Assert.Equal((nuint)0x102, WindowSpaces.FollowingTheActiveSpace(0x80));
    }

    /// <summary>
    /// AppKit throws - and a throw inside objc_msgSend ends the app - on contradicting pairs: on all
    /// Spaces and moving to the active one, or full screen itself and over full screen. Those go;
    /// anything else the window had stays.
    /// </summary>
    [Theory]
    [InlineData(0x1)]
    [InlineData(0x80)]
    [InlineData(0x200)]
    [InlineData(0x281)]
    public void NothingContradictoryIsKept(int current)
    {
        nuint result = WindowSpaces.FollowingTheActiveSpace((nuint)current);

        Assert.Equal((nuint)0, result & (WindowSpaces.CanJoinAllSpaces | WindowSpaces.FullScreenPrimary | WindowSpaces.FullScreenNone));
        Assert.Equal(WindowSpaces.MoveToActiveSpace | WindowSpaces.FullScreenAuxiliary, result & (WindowSpaces.MoveToActiveSpace | WindowSpaces.FullScreenAuxiliary));
    }

    [Fact]
    public void OtherBehaviourIsKept()
    {
        const nuint stationary = 1 << 4;
        Assert.Equal(stationary | WindowSpaces.MoveToActiveSpace | WindowSpaces.FullScreenAuxiliary, WindowSpaces.FollowingTheActiveSpace(stationary | WindowSpaces.FullScreenPrimary));
    }

    /// <summary>A window not yet made - no native handle - is left alone rather than crashing.</summary>
    [Fact]
    public void NoWindowIsNothingToDo()
    {
        WindowSpaces.FollowActiveSpace(IntPtr.Zero);
        Assert.Null(WindowSpaces.Describe(IntPtr.Zero));
    }
}
