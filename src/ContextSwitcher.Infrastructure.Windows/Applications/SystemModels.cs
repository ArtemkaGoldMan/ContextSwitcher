namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>A process running now.</summary>
/// <param name="Pid">Its id.</param>
/// <param name="ParentPid">The id of the process that started it; the parent may have ended since.</param>
/// <param name="ImagePath">The full path of its executable; null where Windows won't say - a process of another user, or of the system.</param>
/// <param name="PackageFamilyName">The family of its package; null when it isn't a Store app's.</param>
/// <param name="StartedAt">
/// When it started; null where Windows won't say. Windows gives an ended process's id to a later one,
/// so a process is only taken for its parent's child if it started after it.
/// </param>
public sealed record ProcessInfo(int Pid, int ParentPid, string? ImagePath, string? PackageFamilyName, DateTime? StartedAt);

/// <summary>A top-level window, as Windows lists it.</summary>
/// <param name="Handle">The window's handle.</param>
/// <param name="Pid">The process that owns it.</param>
/// <param name="IsVisible">Whether it is shown, as opposed to hidden by its app.</param>
/// <param name="IsCloaked">
/// Whether Windows hides it though it is shown: it is on another virtual desktop, or belongs to a
/// Store app that is suspended.
/// </param>
/// <param name="IsMinimized">Whether it is minimised to the taskbar.</param>
/// <param name="HasOwner">Whether another window owns it: a dialog does, a main window doesn't.</param>
/// <param name="IsToolWindow">Whether it is a palette or a tray helper, which the taskbar doesn't list.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
public sealed record WindowInfo(
    nint Handle,
    int Pid,
    bool IsVisible,
    bool IsCloaked,
    bool IsMinimized,
    bool HasOwner,
    bool IsToolWindow,
    int Width,
    int Height);
