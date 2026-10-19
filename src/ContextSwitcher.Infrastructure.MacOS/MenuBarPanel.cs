using System.Runtime.InteropServices;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Turns the menu bar popover's window into a panel that doesn't bring the app forward - the kind of
/// window menu bar apps open. An ordinary window has to activate its app to be shown with focus, and
/// an app with a Dock icon coming forward makes macOS leave a full-screen app for a desktop: "Open
/// Dashboard" threw the user onto another desktop instead of showing over what they were doing.
///
/// Measured in the running app (CONTEXTSWITCHER_DIAGNOSE=windows) with it in the background, as it
/// is when the menu bar is used: giving the popover the keyboard as it opens activates the app - even
/// for a panel AppKit itself made non-activating from the start - while showing it without focus
/// leaves the app where it was, the popover on the desktop in front and over everything. So it opens
/// without focus; a click in it gives it focus, and a click outside or another app coming forward
/// closes it as before.
///
/// Avalonia makes its windows of class <c>AvnWindow</c> (an NSWindow) and its pop-ups of class
/// <c>AvnPanel</c> (an NSPanel) from one source, so the two have the same fields at the same offsets
/// and the same methods, bar <c>canBecomeMainWindow</c>, which a popover doesn't want. The window is
/// moved over to <c>AvnPanel</c> only when exactly that holds; anything else stays an ordinary window,
/// shown and activated as before.
/// </summary>
public static class MenuBarPanel
{
    /// <summary><c>NSWindowStyleMaskNonactivatingPanel</c>.</summary>
    public const nuint NonactivatingPanelStyle = 1 << 7;

    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    /// <summary>
    /// Makes <paramref name="nsWindow"/> a non-activating panel, or confirms it already is one.
    /// <see langword="false"/> leaves it an ordinary window, to be shown and activated as one.
    /// </summary>
    public static bool Make(IntPtr nsWindow)
    {
        if (nsWindow == IntPtr.Zero || !OperatingSystem.IsMacOS())
        {
            return false;
        }

        IntPtr panelClass = objc_getClass("AvnPanel");
        IntPtr windowClass = objc_getClass("AvnWindow");
        IntPtr current = object_getClass(nsWindow);
        if (panelClass == IntPtr.Zero || current == IntPtr.Zero)
        {
            return false;
        }

        if (current != panelClass)
        {
            // Exactly Avalonia's window - not a subclass AppKit made to observe it - and laid out
            // exactly as its panel is.
            if (current != windowClass || class_getInstanceSize(current) != class_getInstanceSize(panelClass))
            {
                return false;
            }

            object_setClass(nsWindow, panelClass);
        }

        nuint style = SendReturningNUInt(nsWindow, sel_registerName("styleMask"));
        SendNUInt(nsWindow, sel_registerName("setStyleMask:"), style | NonactivatingPanelStyle);
        SendBool(nsWindow, sel_registerName("setHidesOnDeactivate:"), 0);
        SendBool(nsWindow, sel_registerName("setBecomesKeyOnlyIfNeeded:"), 0);

        // The style alone, set after the window was made, doesn't reach the window server; this is
        // what AppKit sets for a panel made non-activating from the start, so a click in it doesn't
        // activate the app either. Private, so only if it's there.
        IntPtr preventsActivation = sel_registerName("_setPreventsActivation:");
        if (SendReturningByte(nsWindow, sel_registerName("respondsToSelector:"), preventsActivation) != 0)
        {
            SendBool(nsWindow, preventsActivation, 1);
        }

        return true;
    }

    /// <summary>
    /// What the log says about the popover: its class, whether it has the keyboard, and whether the
    /// app came forward - which is what makes macOS leave a full-screen app.
    /// </summary>
    public static string? Describe(IntPtr nsWindow)
    {
        if (nsWindow == IntPtr.Zero || !OperatingSystem.IsMacOS())
        {
            return null;
        }

        string className = Marshal.PtrToStringUTF8(class_getName(object_getClass(nsWindow))) ?? "?";
        bool isKey = SendReturningByte(nsWindow, sel_registerName("isKeyWindow")) != 0;
        IntPtr app = Send(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
        bool appActive = app != IntPtr.Zero && SendReturningByte(app, sel_registerName("isActive")) != 0;
        bool frontmost = FrontmostApplication.ProcessId() == Environment.ProcessId;
        return $"class={className}, isKeyWindow={isKey}, appActive={appActive}, appFrontmost={frontmost}";
    }

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr object_getClass(IntPtr instance);

    [DllImport(ObjectiveC)]
    private static extern IntPtr object_setClass(IntPtr instance, IntPtr type);

    [DllImport(ObjectiveC)]
    private static extern nuint class_getInstanceSize(IntPtr type);

    [DllImport(ObjectiveC)]
    private static extern IntPtr class_getName(IntPtr type);

    [DllImport(ObjectiveC)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern nuint SendReturningNUInt(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern byte SendReturningByte(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern byte SendReturningByte(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern void SendNUInt(IntPtr receiver, IntPtr selector, nuint value);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern void SendBool(IntPtr receiver, IntPtr selector, byte value);
}
