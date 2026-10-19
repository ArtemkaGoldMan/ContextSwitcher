using System.Runtime.InteropServices;
using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Opening at login through ServiceManagement's <c>SMAppService.mainAppService</c> (macOS 13+), the
/// way macOS itself expects an app to add itself: it shows the app in Login Items, tells the person
/// it was added, and lets them turn it off there. Called through the Objective-C runtime, as .NET has
/// no binding for it. Only an app bundle has a main app service; run from source it is unavailable.
/// </summary>
public sealed class MacLoginItem : ILoginItem
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    private const string Framework = "/System/Library/Frameworks/ServiceManagement.framework/ServiceManagement";

    // SMAppServiceStatus.
    private const long NotRegistered = 0;
    private const long Enabled = 1;
    private const long RequiresApproval = 2;

    /// <inheritdoc />
    public LoginItemStatus Status
    {
        get
        {
            if (!IsAppBundle() || !NativeLibrary.TryLoad(Framework, out _))
            {
                return LoginItemStatus.Unavailable;
            }

            IntPtr pool = objc_autoreleasePoolPush();
            try
            {
                IntPtr service = MainAppService();
                if (service == IntPtr.Zero)
                {
                    return LoginItemStatus.Unavailable;
                }

                return SendReturningLong(service, sel_registerName("status")) switch
                {
                    Enabled => LoginItemStatus.On,
                    RequiresApproval => LoginItemStatus.NeedsApproval,
                    NotRegistered => LoginItemStatus.Off,
                    _ => LoginItemStatus.Off
                };
            }
            finally
            {
                objc_autoreleasePoolPop(pool);
            }
        }
    }

    /// <inheritdoc />
    public string? SetEnabled(bool enabled)
    {
        if (this.Status == LoginItemStatus.Unavailable)
        {
            return null;
        }

        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            string selector = enabled ? "registerAndReturnError:" : "unregisterAndReturnError:";
            bool done = SendWithError(MainAppService(), sel_registerName(selector), out IntPtr error) != 0;
            if (done || error == IntPtr.Zero)
            {
                return null;
            }

            return Text(Send(error, sel_registerName("localizedDescription"))) ?? "macOS gave no reason.";
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    /// <inheritdoc />
    public void OpenSystemSettings()
    {
        if (this.Status == LoginItemStatus.Unavailable)
        {
            return;
        }

        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            Send(objc_getClass("SMAppService"), sel_registerName("openSystemSettingsLoginItems"));
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    private static IntPtr MainAppService() =>
        Send(objc_getClass("SMAppService"), sel_registerName("mainAppService"));

    /// <summary>Whether this process is an app bundle with an identifier - the only kind with a main app service.</summary>
    private static bool IsAppBundle()
    {
        if (!OperatingSystem.IsMacOSVersionAtLeast(13))
        {
            return false;
        }

        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            IntPtr bundle = Send(objc_getClass("NSBundle"), sel_registerName("mainBundle"));
            return bundle != IntPtr.Zero
                && Send(bundle, sel_registerName("bundleIdentifier")) != IntPtr.Zero
                && Text(Send(bundle, sel_registerName("bundlePath")))?.EndsWith(".app", StringComparison.Ordinal) == true;
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    private static string? Text(IntPtr text) =>
        text == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Send(text, sel_registerName("UTF8String")));

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
    private static extern long SendReturningLong(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern byte SendWithError(IntPtr receiver, IntPtr selector, out IntPtr error);
}
