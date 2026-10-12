using System.Runtime.InteropServices;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// macOS notifications through UserNotifications' UNUserNotificationCenter, called through the
/// Objective-C runtime: .NET has no binding for it.
///
/// macOS asks the person once whether the app may notify them. Each <see cref="Show"/> asks the
/// center for that answer - it only prompts the first time - and shows the notification once it is
/// yes. Clicks come back to a delegate object made at runtime; macOS hands one that launched the app
/// to the delegate set while it starts, so this is created at startup.
///
/// The center only works in an app bundle: anywhere else it throws an Objective-C exception, which
/// takes the process down past any .NET handler. Run from source, this does nothing at all.
/// </summary>
public sealed class MacNotifications : ISystemNotifications
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    private const string Framework = "/System/Library/Frameworks/UserNotifications.framework/UserNotifications";
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";
    private const string DelegateClassName = "ContextSwitcherNotificationDelegate";

    /// <summary>UNAuthorizationOptionAlert: banners and Notification Center, no sound or badge.</summary>
    private const nuint AlertOption = 1 << 2;

    /// <summary>UNNotificationPresentationOptionList | Banner: shown while the app is in front too.</summary>
    private const nuint ListAndBanner = (1 << 3) | (1 << 4);

    private const int BlockIsGlobal = 1 << 28;
    private const int BlockHasSignature = 1 << 30;

    // Native code keeps pointers to these for the life of the process; the fields keep the GC off them.
    private static readonly AuthorizationHandler OnAuthorization = HandleAuthorization;
    private static readonly CompletionHandler OnAdded = HandleAdded;
    private static readonly DelegateMethod OnResponse = HandleResponse;
    private static readonly DelegateMethod OnWillPresent = HandleWillPresent;

    /// <summary>The instance the callbacks report to: the one set up in an app bundle.</summary>
    private static MacNotifications? active;

    private readonly ILogger logger;
    private readonly IClock clock;
    private readonly object gate = new();
    private readonly Dictionary<string, (string Title, string Body)> waiting = [];
    private readonly IntPtr authorizationBlock;
    private readonly IntPtr addedBlock;

    public MacNotifications(ILogger logger, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        this.logger = logger;
        this.clock = clock;

        if (active is not null || !IsAppBundle() || !NativeLibrary.TryLoad(Framework, out _))
        {
            return;
        }

        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            IntPtr center = Center();
            if (center == IntPtr.Zero)
            {
                return;
            }

            // Never released: the center holds its delegate weakly.
            Send(center, Selector("setDelegate:"), CreateDelegate());
            this.authorizationBlock = CreateGlobalBlock(Marshal.GetFunctionPointerForDelegate(OnAuthorization), "v@?B@");
            this.addedBlock = CreateGlobalBlock(Marshal.GetFunctionPointerForDelegate(OnAdded), "v@?@");
            active = this;
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    /// <inheritdoc />
    public event EventHandler<string>? Clicked;

    /// <inheritdoc />
    public void Show(string id, string title, string body)
    {
        if (active != this)
        {
            return;
        }

        lock (this.gate)
        {
            this.waiting[id] = (title, body);
        }

        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            SendOptions(Center(), Selector("requestAuthorizationWithOptions:completionHandler:"), AlertOption, this.authorizationBlock);
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    /// <inheritdoc />
    public void Remove(string id)
    {
        if (active != this)
        {
            return;
        }

        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            IntPtr ids = Send(Class("NSArray"), Selector("arrayWithObject:"), Text(id));
            Send(Center(), Selector("removeDeliveredNotificationsWithIdentifiers:"), ids);
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    /// <summary>Whether this process is an app bundle with an identifier, the only kind the center serves.</summary>
    private static bool IsAppBundle()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            IntPtr pool = objc_autoreleasePoolPush();
            try
            {
                IntPtr bundle = Send(Class("NSBundle"), Selector("mainBundle"));
                return bundle != IntPtr.Zero
                    && Send(bundle, Selector("bundleIdentifier")) != IntPtr.Zero
                    && String(Send(bundle, Selector("bundlePath")))?.EndsWith(".app", StringComparison.Ordinal) == true;
            }
            finally
            {
                objc_autoreleasePoolPop(pool);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Runs on a thread of macOS's choosing once it has the person's answer.</summary>
    private void Deliver(bool allowed, IntPtr error)
    {
        List<KeyValuePair<string, (string Title, string Body)>> ready;
        lock (this.gate)
        {
            ready = [.. this.waiting];
            this.waiting.Clear();
        }

        if (!allowed)
        {
            string why = error == IntPtr.Zero ? "turned off for Context Switcher" : Describe(error);
            this.Log(LogLevel.Information, "NotificationsNotAllowed", $"A notification wasn't shown: notifications are {why}.");
            return;
        }

        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            IntPtr center = Center();
            foreach ((string id, (string title, string body)) in ready)
            {
                IntPtr content = Send(Send(Class("UNMutableNotificationContent"), Selector("alloc")), Selector("init"));
                Send(content, Selector("setTitle:"), Text(title));
                Send(content, Selector("setBody:"), Text(body));
                IntPtr request = Send(Class("UNNotificationRequest"), Selector("requestWithIdentifier:content:trigger:"), Text(id), content, IntPtr.Zero);
                Send(center, Selector("addNotificationRequest:withCompletionHandler:"), request, this.addedBlock);
                Send(content, Selector("release"));
            }
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    private void Log(LogLevel level, string eventId, string message) =>
        _ = this.logger.LogAsync(new LogEntry
        {
            Timestamp = this.clock.UtcNow,
            Level = level,
            Category = "Notifications",
            EventId = eventId,
            Message = message
        });

    // Every callback below is entered from native code, where an exception would end the process
    // rather than reach a handler, so each one stops them.

    private static void HandleAuthorization(IntPtr block, byte granted, IntPtr error)
    {
        try
        {
            active?.Deliver(granted != 0, error);
        }
#pragma warning disable CA1031 // See above: nothing may escape into native code.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private static void HandleAdded(IntPtr block, IntPtr error)
    {
        try
        {
            if (error == IntPtr.Zero)
            {
                active?.Log(LogLevel.Information, "NotificationShown", "macOS took the notification.");
            }
            else
            {
                active?.Log(LogLevel.Warning, "NotificationNotShown", $"macOS refused the notification: {Describe(error)}");
            }
        }
#pragma warning disable CA1031 // Nothing may escape into native code.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    /// <summary>userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler: - a click.</summary>
    private static void HandleResponse(IntPtr self, IntPtr selector, IntPtr center, IntPtr response, IntPtr completionHandler)
    {
        try
        {
            IntPtr pool = objc_autoreleasePoolPush();
            try
            {
                IntPtr request = Send(Send(response, Selector("notification")), Selector("request"));
                if (String(Send(request, Selector("identifier"))) is { } id && active is { } notifications)
                {
                    notifications.Clicked?.Invoke(notifications, id);
                }
            }
            finally
            {
                objc_autoreleasePoolPop(pool);
            }
        }
#pragma warning disable CA1031 // Nothing may escape into native code.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
        finally
        {
            CallBlock(completionHandler);
        }
    }

    /// <summary>
    /// userNotificationCenter:willPresentNotification:withCompletionHandler: - without it a
    /// notification is dropped while the app is in front, a window of it open.
    /// </summary>
    private static void HandleWillPresent(IntPtr self, IntPtr selector, IntPtr center, IntPtr notification, IntPtr completionHandler)
    {
        try
        {
            CallBlock(completionHandler, ListAndBanner);
        }
#pragma warning disable CA1031 // Nothing may escape into native code.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    /// <summary>An instance of a class made here, answering the two delegate methods above.</summary>
    private static IntPtr CreateDelegate()
    {
        IntPtr type = Class(DelegateClassName);
        if (type == IntPtr.Zero)
        {
            type = objc_allocateClassPair(Class("NSObject"), DelegateClassName, 0);
            class_addMethod(
                type,
                Selector("userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:"),
                Marshal.GetFunctionPointerForDelegate(OnResponse),
                "v@:@@@?");
            class_addMethod(
                type,
                Selector("userNotificationCenter:willPresentNotification:withCompletionHandler:"),
                Marshal.GetFunctionPointerForDelegate(OnWillPresent),
                "v@:@@@?");
            if (objc_getProtocol("UNUserNotificationCenterDelegate") is var protocol && protocol != IntPtr.Zero)
            {
                class_addProtocol(type, protocol);
            }

            objc_registerClassPair(type);
        }

        return Send(Send(type, Selector("alloc")), Selector("init"));
    }

    /// <summary>
    /// An Objective-C block that calls <paramref name="invoke"/>, laid out as the compiler lays out
    /// a global one: copying or releasing it does nothing, so it can live in unmanaged memory for good.
    /// </summary>
    private static IntPtr CreateGlobalBlock(IntPtr invoke, string signature)
    {
        IntPtr descriptor = Marshal.AllocHGlobal(3 * IntPtr.Size);
        Marshal.WriteInt64(descriptor, 0, 0);
        Marshal.WriteInt64(descriptor, 8, 32);
        Marshal.WriteIntPtr(descriptor, 16, Marshal.StringToCoTaskMemUTF8(signature));

        IntPtr block = Marshal.AllocHGlobal(32);
        Marshal.WriteIntPtr(block, 0, NativeLibrary.GetExport(NativeLibrary.Load(LibSystem), "_NSConcreteGlobalBlock"));
        Marshal.WriteInt32(block, 8, BlockIsGlobal | BlockHasSignature);
        Marshal.WriteInt32(block, 12, 0);
        Marshal.WriteIntPtr(block, 16, invoke);
        Marshal.WriteIntPtr(block, 24, descriptor);
        return block;
    }

    /// <summary>Calls a block macOS handed over: its function sits 16 bytes in, and takes the block first.</summary>
    private static void CallBlock(IntPtr block)
    {
        if (block != IntPtr.Zero)
        {
            Marshal.GetDelegateForFunctionPointer<VoidBlock>(Marshal.ReadIntPtr(block, 16))(block);
        }
    }

    private static void CallBlock(IntPtr block, nuint options)
    {
        if (block != IntPtr.Zero)
        {
            Marshal.GetDelegateForFunctionPointer<OptionsBlock>(Marshal.ReadIntPtr(block, 16))(block, options);
        }
    }

    private static IntPtr Center() => Send(Class("UNUserNotificationCenter"), Selector("currentNotificationCenter"));

    private static string Describe(IntPtr error) => String(Send(error, Selector("localizedDescription"))) ?? "an unknown error";

    private static IntPtr Text(string text) => SendText(Class("NSString"), Selector("stringWithUTF8String:"), text);

    private static string? String(IntPtr text) =>
        text == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Send(text, Selector("UTF8String")));

    private static IntPtr Class(string name) => objc_getClass(name);

    private static IntPtr Selector(string name) => sel_registerName(name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AuthorizationHandler(IntPtr block, byte granted, IntPtr error);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CompletionHandler(IntPtr block, IntPtr error);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DelegateMethod(IntPtr self, IntPtr selector, IntPtr center, IntPtr subject, IntPtr completionHandler);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void VoidBlock(IntPtr block);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OptionsBlock(IntPtr block, nuint options);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_getProtocol([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_allocateClassPair(IntPtr superclass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, nuint extraBytes);

    [DllImport(ObjectiveC)]
    private static extern void objc_registerClassPair(IntPtr type);

    [DllImport(ObjectiveC)]
    private static extern byte class_addMethod(IntPtr type, IntPtr selector, IntPtr implementation, [MarshalAs(UnmanagedType.LPUTF8Str)] string types);

    [DllImport(ObjectiveC)]
    private static extern byte class_addProtocol(IntPtr type, IntPtr protocol);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_autoreleasePoolPush();

    [DllImport(ObjectiveC)]
    private static extern void objc_autoreleasePoolPop(IntPtr pool);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr first, IntPtr second);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr first, IntPtr second, IntPtr third);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern void SendOptions(IntPtr receiver, IntPtr selector, nuint options, IntPtr block);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendText(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);
}
