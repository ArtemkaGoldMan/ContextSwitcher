using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Platform;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>
/// A popover window the system says nothing about, as off a Mac: it shows as an ordinary window, at the
/// screen's top left corner.
/// </summary>
public sealed class FakePopoverWindow : IPopoverWindow
{
    public bool Prepare(IntPtr window) => false;

    public string? Describe(IntPtr window) => null;

    public int? FrontmostProcessId() => null;

    public bool? IsMouseDown() => null;

    public bool? IsPointerOutside(IntPtr window, double left, double top, double right, double bottom) => null;

    public WindowPosition Place(PopoverScreen screen, PopoverSize size) => new(screen.WorkingArea.X, screen.WorkingArea.Y);
}
