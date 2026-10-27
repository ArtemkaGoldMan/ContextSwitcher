namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>What counts as a window on screen, and which processes are never ended.</summary>
/// <param name="SmallestWindow">A window smaller than this, on either side, is not one anyone works in.</param>
/// <param name="NeverEnded">
/// File names of processes that Force quit leaves alone whatever app is named: ending one signs the
/// person out, hides the taskbar or stops Windows.
/// </param>
public sealed record AppProcessesOptions(int SmallestWindow, IReadOnlyList<string> NeverEnded)
{
    /// <summary>
    /// A "save changes?" box is bigger than 100 pixels, and the strips and 1×1 helpers apps keep are
    /// smaller. The names are the processes Windows can't run without; <c>explorer.exe</c> is the
    /// taskbar and the desktop, and a Start menu shortcut can point at it.
    /// </summary>
    public static AppProcessesOptions Default { get; } = new(
        100,
        [
            "explorer.exe", "winlogon.exe", "csrss.exe", "wininit.exe", "services.exe", "lsass.exe",
            "smss.exe", "svchost.exe", "dwm.exe", "fontdrvhost.exe", "sihost.exe", "taskhostw.exe"
        ]);
}
