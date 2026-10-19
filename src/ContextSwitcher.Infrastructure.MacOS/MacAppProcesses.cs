using System.Runtime.InteropServices;
using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Finds an app among <c>NSWorkspace.runningApplications</c> by the name a profile stores - its
/// bundle's or its own, as <see cref="MacApplicationHider"/> does - and asks about its process:
/// <list type="bullet">
/// <item>whether it still runs: <c>kill(pid, 0)</c>, as the workspace's list only changes when the
/// main run loop turns, and could still list an app that has gone;</item>
/// <item>whether it has a window on screen: <c>CGWindowListCopyWindowInfo</c>, whose owner, layer,
/// alpha and size need no Screen Recording permission - only window titles do, and none are read.
/// Only the windows on screen now count. A window on another desktop can't be told from one an app
/// keeps hidden for later - Steam keeps its menus that way, and they look the same in every way
/// macOS will say, even through its private desktop calls - so the caller says "or its window is on
/// another desktop" rather than guess. Windows of the app's child processes are its own: Steam draws
/// every window it shows in a "Steam Helper" it started, and its own process has only hidden ones;</item>
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
    private const string LibSystem = "/usr/lib/libSystem.dylib";

    /// <summary>More processes than any app starts: a bound on walking its children.</summary>
    private const int MostProcesses = 1024;

    private const uint OnScreenOnly = 1 << 0;
    private const uint ExcludeDesktopElements = 1 << 4;
    private const int NumberSInt32 = 3;
    private const int NumberDouble = 13;
    private const uint Utf8 = 0x08000100;
    private const int NoSuchProcess = 3;

    /// <summary>
    /// Smaller than this on either side is not a window anyone works in - the 1×1 and strip helpers
    /// some apps keep - so it does not count. A "save changes?" alert is bigger.
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
    public bool? HasWindowOnScreen(string app)
    {
        if (Find(app) is not { Count: > 0 } found || !NativeLibrary.TryLoad(CoreGraphics, out _))
        {
            return null;
        }

        HashSet<int> alive = found.Where(entry => IsAlive(entry.Pid)).Select(entry => entry.Pid).ToHashSet();
        if (alive.Count == 0)
        {
            return false;
        }

        HashSet<int> pids = WithChildren(alive);

        IntPtr windows = CGWindowListCopyWindowInfo(OnScreenOnly | ExcludeDesktopElements, 0);
        if (windows == IntPtr.Zero)
        {
            return null;
        }

        IntPtr ownerKey = Key("kCGWindowOwnerPID");
        IntPtr layerKey = Key("kCGWindowLayer");
        IntPtr alphaKey = Key("kCGWindowAlpha");
        IntPtr boundsKey = Key("kCGWindowBounds");
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
                    && rect.Width >= SmallestWindow && rect.Height >= SmallestWindow)
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            Release(boundsKey);
            Release(alphaKey);
            Release(layerKey);
            Release(ownerKey);
            CFRelease(windows);
        }
    }

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

    /// <summary><paramref name="roots"/> and every process they started, and those started, and so on.</summary>
    private static HashSet<int> WithChildren(HashSet<int> roots)
    {
        HashSet<int> all = [.. roots];
        Queue<int> parents = new(roots);
        int[] children = new int[MostProcesses];
        while (parents.Count > 0 && all.Count < MostProcesses)
        {
            int count = proc_listchildpids(parents.Dequeue(), children, children.Length * sizeof(int));
            for (int i = 0; i < Math.Min(count, children.Length); i++)
            {
                if (children[i] > 0 && all.Add(children[i]))
                {
                    parents.Enqueue(children[i]);
                }
            }
        }

        return all;
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
        if (!NativeLibrary.TryLoad(AppKit, out _))
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

    /// <summary>Fills <paramref name="buffer"/> with the ids of the processes <paramref name="parent"/> started; returns how many.</summary>
    [DllImport(LibSystem)]
    private static extern int proc_listchildpids(int parent, int[] buffer, int bufferSize);

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [DllImport(CoreGraphics)]
    private static extern byte CGRectMakeWithDictionaryRepresentation(IntPtr dictionary, out Rect rect);

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
