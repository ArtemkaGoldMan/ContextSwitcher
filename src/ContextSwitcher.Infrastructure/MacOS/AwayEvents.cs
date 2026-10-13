using System.Runtime.InteropServices;
using ContextSwitcher.Core.Analytics;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Hears from macOS when nobody can be using the Mac, and when that ends:
/// <list type="bullet">
/// <item>sleep and wake - NSWorkspace's <c>NSWorkspaceWillSleepNotification</c> and <c>…DidWake…</c>;</item>
/// <item>the screen locking and unlocking - the distributed <c>com.apple.screenIsLocked</c> and
/// <c>com.apple.screenIsUnlocked</c>, which macOS has posted for as long as it has had a lock screen;</item>
/// <item>another user's session coming to the front and leaving - NSWorkspace's
/// <c>NSWorkspaceSessionDidResignActiveNotification</c> and <c>…DidBecomeActive…</c>.</item>
/// </list>
/// They are observed by an object of a class made at runtime, as .NET has no binding for them, and
/// arrive on the main thread. Where AppKit can't be reached - not macOS - nothing is ever raised.
/// </summary>
public sealed class AwayEvents
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string ObserverClassName = "ContextSwitcherAwayObserver";

    // Native code keeps pointers to these for the life of the process; the fields keep the GC off them.
    private static readonly NotificationMethod OnWillSleep = (_, _, _) => Raise(away: true, AwayReason.Sleep);
    private static readonly NotificationMethod OnDidWake = (_, _, _) => Raise(away: false, AwayReason.Sleep);
    private static readonly NotificationMethod OnLocked = (_, _, _) => Raise(away: true, AwayReason.ScreenLocked);
    private static readonly NotificationMethod OnUnlocked = (_, _, _) => Raise(away: false, AwayReason.ScreenLocked);
    private static readonly NotificationMethod OnSessionResigned = (_, _, _) => Raise(away: true, AwayReason.OtherUser);
    private static readonly NotificationMethod OnSessionActivated = (_, _, _) => Raise(away: false, AwayReason.OtherUser);

    /// <summary>The instance the observer reports to; there is one per process.</summary>
    private static AwayEvents? active;

    public AwayEvents()
    {
        if (active is not null || !OperatingSystem.IsMacOS() || !NativeLibrary.TryLoad(AppKit, out IntPtr appKit))
        {
            return;
        }

        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            IntPtr workspace = Send(Send(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace")), sel_registerName("notificationCenter"));
            IntPtr distributed = Send(objc_getClass("NSDistributedNotificationCenter"), sel_registerName("defaultCenter"));
            if (workspace == IntPtr.Zero || distributed == IntPtr.Zero)
            {
                return;
            }

            // Never released: a notification center doesn't keep its observers alive.
            IntPtr observer = CreateObserver();
            Observe(workspace, observer, "macWillSleep:", NotificationName(appKit, "NSWorkspaceWillSleepNotification"));
            Observe(workspace, observer, "macDidWake:", NotificationName(appKit, "NSWorkspaceDidWakeNotification"));
            Observe(workspace, observer, "sessionResigned:", NotificationName(appKit, "NSWorkspaceSessionDidResignActiveNotification"));
            Observe(workspace, observer, "sessionActivated:", NotificationName(appKit, "NSWorkspaceSessionDidBecomeActiveNotification"));
            Observe(distributed, observer, "screenLocked:", Text("com.apple.screenIsLocked"));
            Observe(distributed, observer, "screenUnlocked:", Text("com.apple.screenIsUnlocked"));
            active = this;
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    /// <summary>Nobody can be using the Mac, for the reason given.</summary>
    public event EventHandler<AwayReason>? Away;

    /// <summary>That reason no longer holds.</summary>
    public event EventHandler<AwayReason>? Back;

    /// <summary>Whether macOS will tell this instance - the first one made, on a Mac.</summary>
    public bool IsListening => active == this;

    // Entered from native code, where an exception would end the process rather than reach a
    // handler, so it stops them.
    private static void Raise(bool away, AwayReason reason)
    {
        try
        {
            if (active is { } events)
            {
                (away ? events.Away : events.Back)?.Invoke(events, reason);
            }
        }
#pragma warning disable CA1031 // Nothing may escape into native code.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private static void Observe(IntPtr center, IntPtr observer, string selector, IntPtr name)
    {
        if (name != IntPtr.Zero)
        {
            SendObserve(center, sel_registerName("addObserver:selector:name:object:"), observer, sel_registerName(selector), name, IntPtr.Zero);
        }
    }

    /// <summary>The value of an AppKit <c>NSString *const</c>, looked up by its symbol.</summary>
    private static IntPtr NotificationName(IntPtr appKit, string symbol) =>
        NativeLibrary.TryGetExport(appKit, symbol, out IntPtr address) ? Marshal.ReadIntPtr(address) : IntPtr.Zero;

    private static IntPtr Text(string text) =>
        SendText(objc_getClass("NSString"), sel_registerName("stringWithUTF8String:"), text);

    private static IntPtr CreateObserver()
    {
        IntPtr type = objc_getClass(ObserverClassName);
        if (type == IntPtr.Zero)
        {
            type = objc_allocateClassPair(objc_getClass("NSObject"), ObserverClassName, 0);
            foreach ((string selector, NotificationMethod method) in new[]
            {
                ("macWillSleep:", OnWillSleep),
                ("macDidWake:", OnDidWake),
                ("screenLocked:", OnLocked),
                ("screenUnlocked:", OnUnlocked),
                ("sessionResigned:", OnSessionResigned),
                ("sessionActivated:", OnSessionActivated)
            })
            {
                class_addMethod(type, sel_registerName(selector), Marshal.GetFunctionPointerForDelegate(method), "v@:@");
            }

            objc_registerClassPair(type);
        }

        return Send(Send(type, sel_registerName("alloc")), sel_registerName("init"));
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
    private static extern IntPtr SendText(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern void SendObserve(IntPtr receiver, IntPtr selector, IntPtr observer, IntPtr action, IntPtr name, IntPtr sender);
}
