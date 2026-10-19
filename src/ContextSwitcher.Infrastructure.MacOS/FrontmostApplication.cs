using ContextSwitcher.Core.Logging;
using System.Runtime.InteropServices;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Asks macOS which application is frontmost, through <c>NSWorkspace.frontmostApplication</c>.
///
/// Exists so the menu bar popover can close when the user clicks somewhere else. The obvious signal -
/// the popover losing focus - only arrives if it had focus first, and macOS only hands an app focus
/// in response to a real user action, so that cannot be relied on (or even observed from a test).
/// Which app is frontmost can.
/// </summary>
public static class FrontmostApplication
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    /// <summary>
    /// The frontmost application's process id, or <see langword="null"/> when it cannot be read - not
    /// on macOS, or AppKit not loaded, as in a test host.
    /// </summary>
    public static int? ProcessId()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        try
        {
            IntPtr workspaceClass = objc_getClass("NSWorkspace");
            if (workspaceClass == IntPtr.Zero)
            {
                return null;
            }

            IntPtr workspace = SendReturningPointer(workspaceClass, sel_registerName("sharedWorkspace"));
            IntPtr application = workspace == IntPtr.Zero ? IntPtr.Zero : SendReturningPointer(workspace, sel_registerName("frontmostApplication"));
            return application == IntPtr.Zero ? null : SendReturningInt(application, sel_registerName("processIdentifier"));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            AppLog.MissingApi("the frontmost application lookup", ex);
            return null;
        }
    }

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendReturningPointer(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern int SendReturningInt(IntPtr receiver, IntPtr selector);
}
