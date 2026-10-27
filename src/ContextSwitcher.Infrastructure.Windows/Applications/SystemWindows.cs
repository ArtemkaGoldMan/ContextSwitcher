using System.Runtime.InteropServices;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// The top-level windows of the desktop this app runs on, from <c>EnumWindows</c>. Each is asked for
/// its process, whether it is shown (<c>IsWindowVisible</c>), cloaked (DWM's <c>DWMWA_CLOAKED</c>),
/// minimised, owned by another window, a tool window, and its size; closing one is a posted
/// <c>WM_CLOSE</c>, which returns at once and leaves the app to answer.
/// </summary>
public sealed class SystemWindows : ISystemWindows
{
    private const int CloakedAttribute = 14;
    private const int ExtendedStyle = -20;
    private const int ToolWindowStyle = 0x80;
    private const uint GetOwner = 4;
    private const uint Close = 0x0010;

    /// <summary><c>SW_SHOWMINNOACTIVE</c>: minimised, and the app in front stays in front.</summary>
    private const int MinimizeNoActivate = 7;

    /// <summary><c>SW_RESTORE</c>: a minimised window back to the size it had.</summary>
    private const int RestoreWindow = 9;

    private readonly ILogger logger;

    public SystemWindows(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<WindowInfo>? List()
    {
        List<WindowInfo> windows = [];
        bool listed = EnumWindows(
            (handle, _) =>
            {
                windows.Add(Describe(handle));
                return true;
            },
            0);
        if (!listed)
        {
            this.logger.Warn("App", "WindowsNotListed", $"Windows wouldn't list the windows open (error {Marshal.GetLastPInvokeError()}).");
            return null;
        }

        return windows;
    }

    /// <inheritdoc />
    public bool RequestClose(nint handle) =>
        // Not a window, no message: a null handle would post to this app's own thread, and
        // HWND_BROADCAST (0xFFFF) would post the close to every window on the desktop.
        IsWindow(handle) && PostMessageW(handle, Close, 0, 0);

    /// <inheritdoc />
    public bool Minimize(nint handle) => IsWindow(handle) && ShowWindowAsync(handle, MinimizeNoActivate);

    /// <inheritdoc />
    public bool Restore(nint handle) => IsWindow(handle) && ShowWindowAsync(handle, RestoreWindow);

    private static WindowInfo Describe(nint handle)
    {
        _ = GetWindowThreadProcessId(handle, out uint pid);
        _ = DwmGetWindowAttribute(handle, CloakedAttribute, out int cloaked, sizeof(int));
        bool hasRect = GetWindowRect(handle, out Rect rect);
        return new WindowInfo(
            handle,
            (int)pid,
            IsWindowVisible(handle),
            cloaked != 0,
            IsIconic(handle),
            GetWindow(handle, GetOwner) != 0,
            (GetWindowLongW(handle, ExtendedStyle) & ToolWindowStyle) != 0,
            hasRect ? rect.Right - rect.Left : 0,
            hasRect ? rect.Bottom - rect.Top : 0);
    }

    private delegate bool EnumWindowsCallback(nint handle, nint parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint command);

    [DllImport("user32.dll")]
    private static extern int GetWindowLongW(nint window, int index);

    /// <summary>Asks the window's own thread to show it, so that a window that doesn't answer can't stop this app.</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(nint window, int command);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint window, uint message, nint wParam, nint lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
}
