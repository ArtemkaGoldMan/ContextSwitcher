using System.Runtime.InteropServices;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Hears from macOS when the Mac goes to sleep and when it wakes: NSWorkspace's
/// <c>NSWorkspaceWillSleepNotification</c> and <c>NSWorkspaceDidWakeNotification</c>, observed by an
/// object of a class made at runtime, since .NET has no binding for them. Both arrive on the main
/// thread. Where AppKit can't be reached - not macOS - nothing is ever raised.
/// </summary>
public sealed class SleepEvents
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string ObserverClassName = "ContextSwitcherSleepObserver";

    // Native code keeps pointers to these for the life of the process; the fields keep the GC off them.
    private static readonly NotificationMethod OnWillSleep = HandleWillSleep;
    private static readonly NotificationMethod OnDidWake = HandleDidWake;

    /// <summary>The instance the observer reports to; there is one per process.</summary>
    private static SleepEvents? active;

    public SleepEvents()
    {
        if (active is not null || !OperatingSystem.IsMacOS() || !NativeLibrary.TryLoad(AppKit, out IntPtr appKit))
        {
            return;
        }

        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            IntPtr center = Send(Send(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace")), sel_registerName("notificationCenter"));
            IntPtr willSleep = NotificationName(appKit, "NSWorkspaceWillSleepNotification");
            IntPtr didWake = NotificationName(appKit, "NSWorkspaceDidWakeNotification");
            if (center == IntPtr.Zero || willSleep == IntPtr.Zero || didWake == IntPtr.Zero)
            {
                return;
            }

            // Never released: the center doesn't keep its observers alive.
            IntPtr observer = CreateObserver();
            SendObserve(center, sel_registerName("addObserver:selector:name:object:"), observer, sel_registerName("macWillSleep:"), willSleep, IntPtr.Zero);
            SendObserve(center, sel_registerName("addObserver:selector:name:object:"), observer, sel_registerName("macDidWake:"), didWake, IntPtr.Zero);
            active = this;
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    /// <summary>The Mac is about to sleep.</summary>
    public event EventHandler? WillSleep;

    /// <summary>The Mac has woken.</summary>
    public event EventHandler? DidWake;

    /// <summary>Whether macOS will tell this instance about sleep - the first one made, on a Mac.</summary>
    public bool IsListening => active == this;

    /// <summary>The value of an AppKit <c>NSString *const</c>, looked up by its symbol.</summary>
    private static IntPtr NotificationName(IntPtr appKit, string symbol) =>
        NativeLibrary.TryGetExport(appKit, symbol, out IntPtr address) ? Marshal.ReadIntPtr(address) : IntPtr.Zero;

    private static IntPtr CreateObserver()
    {
        IntPtr type = objc_getClass(ObserverClassName);
        if (type == IntPtr.Zero)
        {
            type = objc_allocateClassPair(objc_getClass("NSObject"), ObserverClassName, 0);
            class_addMethod(type, sel_registerName("macWillSleep:"), Marshal.GetFunctionPointerForDelegate(OnWillSleep), "v@:@");
            class_addMethod(type, sel_registerName("macDidWake:"), Marshal.GetFunctionPointerForDelegate(OnDidWake), "v@:@");
            objc_registerClassPair(type);
        }

        return Send(Send(type, sel_registerName("alloc")), sel_registerName("init"));
    }

    // Entered from native code, where an exception would end the process rather than reach a
    // handler, so each one stops them.

    private static void HandleWillSleep(IntPtr self, IntPtr selector, IntPtr notification)
    {
        try
        {
            active?.WillSleep?.Invoke(active, EventArgs.Empty);
        }
#pragma warning disable CA1031 // Nothing may escape into native code.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private static void HandleDidWake(IntPtr self, IntPtr selector, IntPtr notification)
    {
        try
        {
            active?.DidWake?.Invoke(active, EventArgs.Empty);
        }
#pragma warning disable CA1031 // Nothing may escape into native code.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NotificationMethod(IntPtr self, IntPtr selector, IntPtr notification);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_allocateClassPair(IntPtr superclass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, nuint extraBytes);

    [DllImport(ObjectiveC)]
    private static extern void objc_registerClassPair(IntPtr type);

    [DllImport(ObjectiveC)]
    private static extern byte class_addMethod(IntPtr type, IntPtr selector, IntPtr implementation, [MarshalAs(UnmanagedType.LPUTF8Str)] string types);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_autoreleasePoolPush();

    [DllImport(ObjectiveC)]
    private static extern void objc_autoreleasePoolPop(IntPtr pool);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern void SendObserve(IntPtr receiver, IntPtr selector, IntPtr observer, IntPtr action, IntPtr name, IntPtr sender);
}
