namespace ContextSwitcher.Core.Platform;

/// <summary>A rectangle on a screen, in the screen's pixels.</summary>
public readonly record struct ScreenArea(int X, int Y, int Width, int Height)
{
    /// <summary>The first pixel to the right of it.</summary>
    public int Right => this.X + this.Width;

    /// <summary>The first pixel below it.</summary>
    public int Bottom => this.Y + this.Height;
}

/// <summary>Where a window's top left corner goes on a screen, in the screen's pixels.</summary>
public readonly record struct WindowPosition(int X, int Y);

/// <summary>
/// The screen the popover opens on: all of it, the part windows may use - less the menu bar and the
/// Dock, or the taskbar - and how many of its pixels make one of the app's units.
/// </summary>
public sealed record PopoverScreen(ScreenArea Bounds, ScreenArea WorkingArea, double Scaling);

/// <summary>
/// The popover window's size in the app's units, and the transparent room around its card that the
/// card's shadow is drawn into - the card, not the window, is what lines up with the screen's edges.
/// </summary>
public sealed record PopoverSize(double Width, double Height, double ShadowLeft, double ShadowTop, double ShadowRight, double ShadowBottom);

/// <summary>
/// Where the popover's window goes: under the menu bar, or beside the taskbar. Arithmetic only, so
/// each platform's choice is tested on any computer.
/// </summary>
public static class PopoverPlacement
{
    /// <summary>The card's distance from the menu bar, or from the taskbar and the screen's edge.</summary>
    private const int MenuBarGap = 4;

    private const int EdgeGap = 12;

    /// <summary>
    /// Under the menu bar, the card 12 from the right edge and 4 under it. In the units this has always
    /// used, which on macOS place it correctly: the size as it is, without the screen's scaling.
    /// </summary>
    public static WindowPosition UnderMenuBar(PopoverScreen screen, PopoverSize size)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(size);

        ScreenArea area = screen.WorkingArea;
        int x = area.Right - (int)(size.Width - size.ShadowRight) - EdgeGap;
        int y = area.Y + MenuBarGap - (int)size.ShadowTop;
        return new WindowPosition(x, y);
    }

    /// <summary>
    /// Beside the taskbar, at the end where its notification area is - on whichever edge the taskbar
    /// is: the side where the part windows may use falls short of the screen. A taskbar along the top
    /// or bottom has it at the right; one down the left or right side, at the bottom. The card keeps
    /// 12 of the app's units from the taskbar and from the screen's edge. A taskbar that hides itself
    /// leaves no mark, and the popover goes where Windows' own taskbar is: the bottom right.
    /// </summary>
    public static WindowPosition ByTaskbar(PopoverScreen screen, PopoverSize size)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(size);

        ScreenArea all = screen.Bounds;
        ScreenArea area = screen.WorkingArea;
        int Pixels(double units) => (int)Math.Round(units * screen.Scaling);
        int gap = Pixels(EdgeGap);

        bool taskbarOnTheLeft = area.X > all.X;
        bool taskbarAlongTheTop = area.Y > all.Y;

        int x = taskbarOnTheLeft
            ? area.X + gap - Pixels(size.ShadowLeft)
            : area.Right - gap - Pixels(size.Width - size.ShadowRight);
        int y = taskbarAlongTheTop
            ? area.Y + gap - Pixels(size.ShadowTop)
            : area.Bottom - gap - Pixels(size.Height - size.ShadowBottom);
        return new WindowPosition(x, y);
    }
}
