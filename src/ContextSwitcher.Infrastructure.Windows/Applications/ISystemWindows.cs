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

    /// <summary>
    /// Minimises the window to the taskbar, as its minimise button does, without taking the focus
    /// from the app in front. Returns whether the request was sent.
    /// </summary>
    bool Minimize(nint handle);

    /// <summary>Brings a minimised window back, as clicking it on the taskbar does. Returns whether the request was sent.</summary>
    bool Restore(nint handle);
}
