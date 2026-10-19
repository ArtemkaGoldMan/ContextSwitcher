using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// The menu bar popover as an AppKit window: on the Space in front (<see cref="WindowSpaces"/>), a
/// non-activating panel (<see cref="MenuBarPanel"/>), and what AppKit says about the app in front
/// (<see cref="FrontmostApplication"/>) and the mouse (<see cref="OutsideClick"/>).
/// </summary>
public sealed class MacPopoverWindow : IPopoverWindow
{
    /// <inheritdoc />
    public bool Prepare(IntPtr window)
    {
        // By default a window goes back to the Space it was first shown on, so the popover opened out
        // of sight; and taking focus activated the app, which made macOS leave a full-screen app.
        WindowSpaces.FollowActiveSpace(window);
        return MenuBarPanel.Make(window);
    }

    /// <inheritdoc />
    public string? Describe(IntPtr window) =>
        WindowSpaces.Describe(window) is { } space
            ? $"collectionBehavior=0x{space.Behavior:x}, onActiveSpace={space.OnActiveSpace}, {MenuBarPanel.Describe(window)}"
            : null;

    /// <inheritdoc />
    public int? FrontmostProcessId() => FrontmostApplication.ProcessId();

    /// <inheritdoc />
    public bool? IsMouseDown() => OutsideClick.IsMouseDown();

    /// <inheritdoc />
    public bool? IsPointerOutside(IntPtr window, double left, double top, double right, double bottom)
    {
        if (OutsideClick.WindowFrame(window) is not { } frame || OutsideClick.PointerLocation() is not { } pointer)
        {
            return null;
        }

        return !frame.Inset(left, top, right, bottom).Contains(pointer);
    }
}
