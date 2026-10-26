using ContextSwitcher.Core.Platform;

namespace ContextSwitcher.Tests.Platform;

/// <summary>
/// Where the popover opens: under the menu bar on a Mac, as it always has, and beside the taskbar on
/// Windows, on whichever edge the taskbar is.
/// </summary>
public sealed class PopoverPlacementTests
{
    /// <summary>The dashboard: 392 wide, 500 tall, with the room its shadow is drawn into.</summary>
    private static readonly PopoverSize Dashboard = new(392, 500, ShadowLeft: 16, ShadowTop: 10, ShadowRight: 16, ShadowBottom: 22);

    private static readonly ScreenArea FullHd = new(0, 0, 1920, 1080);

    /// <summary>
    /// The card 12 from the right edge and 4 under the menu bar, in the units the Mac always used -
    /// the same arithmetic as before it moved here.
    /// </summary>
    [Fact]
    public void OnAMacTheCardSitsUnderTheMenuBarAtTheRight()
    {
        PopoverScreen mac = new(new ScreenArea(0, 0, 1512, 982), new ScreenArea(0, 33, 1512, 949), Scaling: 2);

        WindowPosition place = PopoverPlacement.UnderMenuBar(mac, Dashboard);

        Assert.Equal(new WindowPosition(1512 - (392 - 16) - 12, 33 + 4 - 10), place);
    }

    /// <summary>Windows' own taskbar, along the bottom: the card 12 above it and 12 from the right edge.</summary>
    [Fact]
    public void ByATaskbarAlongTheBottomTheCardSitsAboveItAtTheRight()
    {
        PopoverScreen screen = new(FullHd, new ScreenArea(0, 0, 1920, 1032), Scaling: 1);

        WindowPosition place = PopoverPlacement.ByTaskbar(screen, Dashboard);

        Assert.Equal(new WindowPosition(1920 - 12 - (392 - 16), 1032 - 12 - (500 - 22)), place);
    }

    [Fact]
    public void ByATaskbarAlongTheTopTheCardSitsUnderItAtTheRight()
    {
        PopoverScreen screen = new(FullHd, new ScreenArea(0, 48, 1920, 1032), Scaling: 1);

        WindowPosition place = PopoverPlacement.ByTaskbar(screen, Dashboard);

        Assert.Equal(new WindowPosition(1920 - 12 - (392 - 16), 48 + 12 - 10), place);
    }

    /// <summary>A taskbar down a side has its notification area at the bottom.</summary>
    [Fact]
    public void ByATaskbarDownTheLeftTheCardSitsBesideItAtTheBottom()
    {
        PopoverScreen screen = new(FullHd, new ScreenArea(62, 0, 1858, 1080), Scaling: 1);

        WindowPosition place = PopoverPlacement.ByTaskbar(screen, Dashboard);

        Assert.Equal(new WindowPosition(62 + 12 - 16, 1080 - 12 - (500 - 22)), place);
    }

    [Fact]
    public void ByATaskbarDownTheRightTheCardSitsBesideItAtTheBottom()
    {
        PopoverScreen screen = new(FullHd, new ScreenArea(0, 0, 1858, 1080), Scaling: 1);

        WindowPosition place = PopoverPlacement.ByTaskbar(screen, Dashboard);

        Assert.Equal(new WindowPosition(1858 - 12 - (392 - 16), 1080 - 12 - (500 - 22)), place);
    }

    /// <summary>A taskbar that hides itself takes nothing from the screen; the card goes where it usually is.</summary>
    [Fact]
    public void ByATaskbarThatHidesTheCardSitsAtTheBottomRight()
    {
        PopoverScreen screen = new(FullHd, FullHd, Scaling: 1);

        WindowPosition place = PopoverPlacement.ByTaskbar(screen, Dashboard);

        Assert.Equal(new WindowPosition(1920 - 12 - (392 - 16), 1080 - 12 - (500 - 22)), place);
    }

    /// <summary>The window is sized in the app's units and the screen counted in pixels: at 150% each unit is 1.5 of them.</summary>
    [Fact]
    public void OnAScaledScreenTheSizeAndTheGapGrowWithIt()
    {
        PopoverScreen screen = new(new ScreenArea(0, 0, 2880, 1620), new ScreenArea(0, 0, 2880, 1548), Scaling: 1.5);

        WindowPosition place = PopoverPlacement.ByTaskbar(screen, Dashboard);

        Assert.Equal(new WindowPosition(2880 - 18 - 564, 1548 - 18 - 717), place);
    }
}
