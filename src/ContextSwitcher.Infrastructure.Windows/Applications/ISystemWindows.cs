namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>The top-level windows Windows has, and asking one to close. Behind an interface so that what is done with them is tested without touching any.</summary>
public interface ISystemWindows
{
    /// <summary>Every top-level window, shown or not; null when Windows can't be asked.</summary>
    IReadOnlyList<WindowInfo>? List();

    /// <summary>
    /// Asks the window to close, as its close button does: <c>WM_CLOSE</c>. The app may ask to save
    /// first, or refuse. Returns whether the request was sent.
    /// </summary>
    bool RequestClose(nint handle);
}
