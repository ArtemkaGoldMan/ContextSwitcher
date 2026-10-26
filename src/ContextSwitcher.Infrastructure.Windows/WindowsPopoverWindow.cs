using System.Runtime.InteropServices;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Platform;

namespace ContextSwitcher.Infrastructure.Windows;

/// <summary>
/// The tray popover on Windows: an ordinary borderless, topmost window with no taskbar button, shown
/// and activated by the taskbar's notification area. Windows lets the app that owns a tray icon take
/// the focus when the icon is clicked, so the window is activated, and closes when it loses the focus
/// as a flyout does. Which app is in front is asked too, so a popover that did not get the focus
/// still closes once another app comes forward.
/// </summary>
public sealed class WindowsPopoverWindow : IPopoverWindow
{
    /// <inheritdoc />
    public bool Prepare(IntPtr window) => false;

    /// <inheritdoc />
    public string? Describe(IntPtr window) => null;

    /// <inheritdoc />
    public int? FrontmostProcessId()
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || GetWindowThreadProcessId(foreground, out uint processId) == 0)
        {
            return null;
        }

        return (int)processId;
    }

    /// <inheritdoc />
    public bool? IsMouseDown() => null;

    /// <inheritdoc />
    public bool? IsPointerOutside(IntPtr window, double left, double top, double right, double bottom) => null;

    /// <inheritdoc />
    public WindowPosition Place(PopoverScreen screen, PopoverSize size) => PopoverPlacement.ByTaskbar(screen, size);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
