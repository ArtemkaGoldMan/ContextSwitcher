using System.Runtime.InteropServices;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Notices a mouse press anywhere on screen outside a window, so the menu bar popover can close the
/// way a menu does - on the desktop, on another app, on the menu bar icon itself.
///
/// Watching which app is frontmost could not do that: a click into the app that was already in
/// front, or on the menu bar, changes nothing there, so the popover stayed open until "Open App".
/// AppKit will tell any process whether a mouse button is down and where the pointer is - no
/// Accessibility permission, unlike watching the events themselves - so polling those works.
///
/// Everything is in AppKit's screen points, origin bottom-left: the pointer and the window's frame
/// come from the same place, so no conversion between coordinate systems can go wrong.
/// </summary>
public static class OutsideClick
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    /// <summary>Whether any mouse button is down right now, or null when AppKit cannot be asked.</summary>
    public static bool? IsMouseDown()
    {
        if (!CanCall())
        {
            return null;
        }

        try
        {
            IntPtr eventClass = objc_getClass("NSEvent");
            return eventClass == IntPtr.Zero ? null : SendReturningNUInt(eventClass, sel_registerName("pressedMouseButtons")) != 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Where the pointer is, in screen points from the bottom-left, or null when unknown.</summary>
    public static ScreenPoint? PointerLocation()
    {
        if (!CanCall())
        {
            return null;
        }

        try
        {
            IntPtr eventClass = objc_getClass("NSEvent");
            return eventClass == IntPtr.Zero ? null : SendReturningPoint(eventClass, sel_registerName("mouseLocation"));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>An NSWindow's frame, in the same screen points as <see cref="PointerLocation"/>.</summary>
    public static ScreenRect? WindowFrame(IntPtr nsWindow)
    {
        if (!CanCall() || nsWindow == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return SendReturningRect(nsWindow, sel_registerName("frame"));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether this reading is a fresh press outside <paramref name="inside"/>: the button has just
    /// gone down - it was up at the last reading - with the pointer outside. A press that began
    /// inside and is dragged out does not count, and neither does a button already held when the
    /// popover opened.
    /// </summary>
    public static bool IsPressOutside(bool wasDown, bool isDown, ScreenPoint pointer, ScreenRect inside) =>
        isDown && !wasDown && !inside.Contains(pointer);

    /// <summary>
    /// Struct returns from objc_msgSend come back in registers only on arm64; on x86_64 a rect
    /// would need objc_msgSend_stret. The app ships for Apple Silicon, so anything else opts out.
    /// </summary>
    private static bool CanCall() =>
        OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern nuint SendReturningNUInt(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern ScreenPoint SendReturningPoint(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern ScreenRect SendReturningRect(IntPtr receiver, IntPtr selector);
}

/// <summary>An AppKit NSPoint: screen points from the bottom-left.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ScreenPoint(double X, double Y);

/// <summary>An AppKit NSRect: origin at its bottom-left corner, in screen points.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public bool Contains(ScreenPoint point) =>
        point.X >= this.X && point.X <= this.X + this.Width && point.Y >= this.Y && point.Y <= this.Y + this.Height;

    /// <summary>The rect shrunk by the given margins, for the card inside the window's shadow room.</summary>
    public ScreenRect Inset(double left, double top, double right, double bottom) =>
        new(this.X + left, this.Y + bottom, this.Width - left - right, this.Height - top - bottom);
}
