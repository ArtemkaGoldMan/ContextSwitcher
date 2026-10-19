using System.Runtime.InteropServices;
using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Shows or hides the app's Dock icon while it runs, through <c>NSApplication.setActivationPolicy:</c>:
/// a regular app has an icon in the Dock and in ⌘-Tab, an accessory app lives in the menu bar only.
/// Avalonia only sets this once, at startup, so a change in Settings used to wait for a restart.
/// </summary>
public sealed class DockIcon : IDockIcon
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    // NSApplicationActivationPolicy.
    private const long Regular = 0;
    private const long Accessory = 1;

    /// <inheritdoc />
    public bool? IsShown()
    {
        IntPtr app = Application();
        return app == IntPtr.Zero ? null : SendReturningLong(app, sel_registerName("activationPolicy")) == Regular;
    }

    /// <summary>Puts the icon in the Dock, or takes it out. Does nothing where AppKit isn't running.</summary>
    public void Show(bool show)
    {
        IntPtr app = Application();
        if (app != IntPtr.Zero)
        {
            SendSettingPolicy(app, sel_registerName("setActivationPolicy:"), show ? Regular : Accessory);
        }
    }

    /// <summary>The running NSApplication, or zero outside a Mac app - in the test host, say.</summary>
    private static IntPtr Application()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return IntPtr.Zero;
        }

        IntPtr type = objc_getClass("NSApplication");
        return type == IntPtr.Zero ? IntPtr.Zero : Send(type, sel_registerName("sharedApplication"));
    }

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern long SendReturningLong(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern byte SendSettingPolicy(IntPtr receiver, IntPtr selector, long policy);
}
