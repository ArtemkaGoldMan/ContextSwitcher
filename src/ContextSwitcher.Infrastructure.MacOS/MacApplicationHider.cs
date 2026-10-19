using System.Runtime.InteropServices;
using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Hides apps through <c>NSRunningApplication.hide</c>, which any app may ask of another - no
/// Automation or Accessibility permission, unlike scripting System Events. An app is found by the
/// name its bundle has in Applications, the name profiles store ("Visual Studio Code"), or by the
/// name it shows in the menu bar ("Code").
///
/// Hiding is a request macOS carries out a moment later. Its answer is no use for telling whether it
/// worked: measured, <c>hide</c> answered NO and <c>isHidden</c> stayed NO for an app that did hide,
/// as this process has no run loop to hear the change. So nothing is reported as failing.
/// </summary>
public sealed class MacApplicationHider : IApplicationHider
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";

    /// <inheritdoc />
    public int Hide(IReadOnlyList<string> apps)
    {
        ArgumentNullException.ThrowIfNull(apps);
        if (apps.Count == 0 || !NativeLibrary.TryLoad(AppKit, out _))
        {
            return 0;
        }

        int asked = 0;
        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            IntPtr running = Send(Send(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace")), sel_registerName("runningApplications"));
            nuint count = running == IntPtr.Zero ? 0 : SendReturningNUInt(running, sel_registerName("count"));
            for (nuint i = 0; i < count; i++)
            {
                IntPtr app = SendWithIndex(running, sel_registerName("objectAtIndex:"), i);
                string? path = Text(Send(Send(app, sel_registerName("bundleURL")), sel_registerName("path")));
                string? bundleName = path is null ? null : Path.GetFileNameWithoutExtension(path);
                string? name = Text(Send(app, sel_registerName("localizedName")));
                if (apps.Any(wanted => Matches(wanted, bundleName, name)))
                {
                    SendReturningByte(app, sel_registerName("hide"));
                    asked++;
                }
            }
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }

        return asked;
    }

    /// <summary>Whether a running app is the one a profile names, by its bundle's name or its own.</summary>
    public static bool Matches(string name, string? bundleName, string? localizedName) =>
        string.Equals(name, bundleName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, localizedName, StringComparison.OrdinalIgnoreCase);

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
    private static extern IntPtr SendWithIndex(IntPtr receiver, IntPtr selector, nuint index);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern nuint SendReturningNUInt(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern byte SendReturningByte(IntPtr receiver, IntPtr selector);
}
