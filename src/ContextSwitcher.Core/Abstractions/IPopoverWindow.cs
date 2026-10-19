namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// How the menu bar popover behaves as a window: where it shows, whether it brings the app forward,
/// and what the system says about clicks elsewhere. Which of them closes it is the window's to decide.
/// Each takes the window's native handle.
/// </summary>
public interface IPopoverWindow
{
    /// <summary>
    /// Readies <paramref name="window"/> to show as the popover: on the desktop in front, over an app
    /// in full screen, and - where it can be - without taking focus, which would bring the app forward.
    /// Returns whether it shows without taking focus; when not, it is shown and activated as an
    /// ordinary window.
    /// </summary>
    bool Prepare(IntPtr window);

    /// <summary>How <paramref name="window"/> shows, for the log; null when that can't be told.</summary>
    string? Describe(IntPtr window);

    /// <summary>The process id of the app in front, or null when that can't be told.</summary>
    int? FrontmostProcessId();

    /// <summary>Whether a mouse button is down, or null when that can't be told.</summary>
    bool? IsMouseDown();

    /// <summary>
    /// Whether the pointer is outside <paramref name="window"/> less the given margins - the room
    /// around the card its shadow is drawn into; null when that can't be told.
    /// </summary>
    bool? IsPointerOutside(IntPtr window, double left, double top, double right, double bottom);
}
