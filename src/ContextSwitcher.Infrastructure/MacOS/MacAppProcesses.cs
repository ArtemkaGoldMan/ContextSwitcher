using System.Runtime.InteropServices;
using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Finds an app among <c>NSWorkspace.runningApplications</c> by the name a profile stores - its
/// bundle's or its own, as <see cref="MacApplicationHider"/> does - and asks about its process:
/// <list type="bullet">
/// <item>whether it still runs: <c>kill(pid, 0)</c>, as the workspace's list only changes when the
/// main run loop turns, and could still list an app that has gone;</item>
/// <item>whether it has a window the person can get to: <c>CGWindowListCopyWindowInfo</c>, whose
/// owner, layer, alpha and size need no Screen Recording permission - only window titles do, and
/// none are read. A window on another desktop counts, not only one on screen: an app asking to save
/// over there must not look windowless. But many apps keep hidden windows that are on no desktop at
/// all - VS Code and Claude do - and those don't count, or an app that closed every window would
/// seem to have one. Which desktop a window is on, macOS only says through the private
/// <c>CGSCopySpacesForWindows</c>, which window tools rely on; should it ever go, every window
/// counts, which errs towards "may be asking to save" and never offers to end an app by force;</item>
/// <item>and ending it: <c>NSRunningApplication.forceTerminate</c>, which any app may ask of another.</item>
/// </list>
/// </summary>
public sealed class MacAppProcesses : IAppProcesses
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string LibC = "libc";

    private const uint AllWindows = 0;
    private const uint ExcludeDesktopElements = 1 << 4;
    private const int AllSpaces = 7;
    private const int NumberSInt32 = 3;
    private const int NumberDouble = 13;
    private const uint Utf8 = 0x08000100;
    private const int NoSuchProcess = 3;

    /// <summary>
    /// Smaller than this on either side is not a window anyone works in - the strips and 1×1 helpers
    /// apps keep along the menu bar - so it does not count. A "save changes?" alert is bigger.
    /// </summary>
    private const double SmallestWindow = 100;

    /// <inheritdoc />
    public bool? IsRunning(string app)
    {
        if (Find(app) is not { } found)
        {
            return null;
        }

        return found.Any(entry => IsAlive(entry.Pid));
    }

    /// <inheritdoc />
    public bool? HasWindow(string app)
    {
        if (Find(app) is not { Count: > 0 } found || !NativeLibrary.TryLoad(CoreGraphics, out _))
        {
            return null;
        }

        HashSet<int> pids = found.Where(entry => IsAlive(entry.Pid)).Select(entry => entry.Pid).ToHashSet();
        if (pids.Count == 0)
        {
            return false;
        }

        IntPtr windows = CGWindowListCopyWindowInfo(AllWindows | ExcludeDesktopElements, 0);
        if (windows == IntPtr.Zero)
        {
            return null;
        }

        IntPtr ownerKey = Key("kCGWindowOwnerPID");
        IntPtr numberKey = Key("kCGWindowNumber");
        IntPtr layerKey = Key("kCGWindowLayer");
        IntPtr alphaKey = Key("kCGWindowAlpha");
        IntPtr boundsKey = Key("kCGWindowBounds");
        IntPtr onScreenKey = Key("kCGWindowIsOnscreen");
        try
        {
            nint count = CFArrayGetCount(windows);
            for (nint i = 0; i < count; i++)
            {
                IntPtr window = CFArrayGetValueAtIndex(windows, i);
                if (Int(window, ownerKey) is { } owner && pids.Contains(owner)
                    && Int(window, layerKey) == 0
                    && (Double(window, alphaKey) ?? 1) > 0
                    && CFDictionaryGetValue(window, boundsKey) is var bounds && bounds != IntPtr.Zero
                    && CGRectMakeWithDictionaryRepresentation(bounds, out Rect rect) != 0
                    && rect.Width >= SmallestWindow && rect.Height >= SmallestWindow
                    && (IsTrue(window, onScreenKey) || IsOnADesktop(Int(window, numberKey)) != false))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            Release(onScreenKey);
            Release(boundsKey);
            Release(alphaKey);
            Release(layerKey);
            Release(numberKey);
            Release(ownerKey);
            CFRelease(windows);
        }
    }

    /// <summary>
    /// Whether a window is on any desktop - this one or another - or null when macOS won't say. A
    /// window an app ordered out to keep for later is on none.
    /// </summary>
    private static bool? IsOnADesktop(int? windowNumber)
    {
        if (windowNumber is not { } number || SpaceFunctions.Value is var callBacks && callBacks == IntPtr.Zero)
        {
            return null;
        }

        IntPtr numberValue = CFNumberCreate(IntPtr.Zero, NumberSInt32, ref number);
        IntPtr list = numberValue == IntPtr.Zero ? IntPtr.Zero : CFArrayCreate(IntPtr.Zero, [numberValue], 1, callBacks);
        try
        {
            IntPtr spaces = list == IntPtr.Zero ? IntPtr.Zero : CGSCopySpacesForWindows(CGSMainConnectionID(), AllSpaces, list);
            if (spaces == IntPtr.Zero)
            {
                return null;
            }

            bool onOne = CFArrayGetCount(spaces) > 0;
            CFRelease(spaces);
            return onOne;
        }
        finally
        {
            Release(list);
            Release(numberValue);
        }
    }

    private static bool IsTrue(IntPtr dictionary, IntPtr key) =>
        CFDictionaryGetValue(dictionary, key) is var value && value != IntPtr.Zero && CFBooleanGetValue(value) != 0;

    /// <inheritdoc />
    public bool ForceQuit(string app)
    {
        bool asked = false;
        Visit(app, (handle, pid) =>
        {
            if (IsAlive(pid))
            {
                SendReturningByte(handle, sel_registerName("forceTerminate"));
                asked = true;
            }
        });
        return asked;
    }

    /// <summary>Whether a process with this id exists; one this user may not signal still does.</summary>
    public static bool IsAlive(int pid) =>
        pid > 0 && (kill(pid, 0) == 0 || Marshal.GetLastPInvokeError() != NoSuchProcess);

    /// <summary>The running apps a profile's name stands for, or null where the workspace can't be asked.</summary>
    private static List<(IntPtr Handle, int Pid)>? Find(string app)
    {
        List<(IntPtr, int)> found = [];
        return Visit(app, (handle, pid) => found.Add((handle, pid))) ? found : null;
    }

    private static bool Visit(string app, Action<IntPtr, int> visit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);
        if (!OperatingSystem.IsMacOS() || !NativeLibrary.TryLoad(AppKit, out _))
        {
            return false;
        }

        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            IntPtr running = Send(Send(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace")), sel_registerName("runningApplications"));
            nuint count = running == IntPtr.Zero ? 0 : SendReturningNUInt(running, sel_registerName("count"));
            for (nuint i = 0; i < count; i++)
            {
                IntPtr handle = SendWithIndex(running, sel_registerName("objectAtIndex:"), i);
                string? path = Text(Send(Send(handle, sel_registerName("bundleURL")), sel_registerName("path")));
                string? bundleName = path is null ? null : Path.GetFileNameWithoutExtension(path);
                string? name = Text(Send(handle, sel_registerName("localizedName")));
                if (MacApplicationHider.Matches(app, bundleName, name))
                {
                    visit(handle, SendReturningInt(handle, sel_registerName("processIdentifier")));
                }
            }

            return true;
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    private static IntPtr Key(string name) => CFStringCreateWithCString(IntPtr.Zero, name, Utf8);

    private static void Release(IntPtr value)
    {
        if (value != IntPtr.Zero)
        {
            CFRelease(value);
        }
    }

    private static int? Int(IntPtr dictionary, IntPtr key) =>
        CFDictionaryGetValue(dictionary, key) is var number && number != IntPtr.Zero && CFNumberGetValue(number, NumberSInt32, out int value) != 0
            ? value
            : null;

    private static double? Double(IntPtr dictionary, IntPtr key) =>
        CFDictionaryGetValue(dictionary, key) is var number && number != IntPtr.Zero && CFNumberGetDoubleValue(number, NumberDouble, out double value) != 0
            ? value
            : null;

    private static string? Text(IntPtr text) =>
        text == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Send(text, sel_registerName("UTF8String")));

    /// <summary>
    /// <c>kCFTypeArrayCallBacks</c> when the private desktop functions are there to be called, or
    /// zero when any is missing - looked up once, as a missing one must never be called.
    /// </summary>
    private static readonly Lazy<IntPtr> SpaceFunctions = new(() =>
        NativeLibrary.TryLoad(CoreGraphics, out IntPtr graphics)
        && NativeLibrary.TryGetExport(graphics, "CGSMainConnectionID", out _)
        && NativeLibrary.TryGetExport(graphics, "CGSCopySpacesForWindows", out _)
        && NativeLibrary.TryLoad(CoreFoundation, out IntPtr foundation)
        && NativeLibrary.TryGetExport(foundation, "kCFTypeArrayCallBacks", out IntPtr callBacks)
            ? callBacks
            : IntPtr.Zero);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Rect
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Width;
        public readonly double Height;
    }

    [DllImport(LibC, SetLastError = true)]
    private static extern int kill(int pid, int signal);

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [DllImport(CoreGraphics)]
    private static extern byte CGRectMakeWithDictionaryRepresentation(IntPtr dictionary, out Rect rect);

    [DllImport(CoreGraphics)]
    private static extern int CGSMainConnectionID();

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGSCopySpacesForWindows(int connection, int mask, IntPtr windows);

    [DllImport(CoreFoundation)]
    private static extern nint CFArrayGetCount(IntPtr array);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);

    [DllImport(CoreFoundation)]
    private static extern byte CFNumberGetValue(IntPtr number, int type, out int value);

    [DllImport(CoreFoundation, EntryPoint = "CFNumberGetValue")]
    private static extern byte CFNumberGetDoubleValue(IntPtr number, int type, out double value);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, uint encoding);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr value);

    [DllImport(CoreFoundation)]
    private static extern byte CFBooleanGetValue(IntPtr value);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFNumberCreate(IntPtr allocator, int type, ref int value);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFArrayCreate(IntPtr allocator, IntPtr[] values, nint count, IntPtr callBacks);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_autoreleasePoolPush();

    [DllImport(ObjectiveC)]
    private static extern void objc_autoreleasePoolPop(IntPtr pool);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendWithIndex(IntPtr receiver, IntPtr selector, nuint index);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern nuint SendReturningNUInt(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern int SendReturningInt(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern byte SendReturningByte(IntPtr receiver, IntPtr selector);
}
