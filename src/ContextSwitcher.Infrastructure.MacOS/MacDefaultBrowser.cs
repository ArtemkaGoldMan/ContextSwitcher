using ContextSwitcher.Core.Logging;
using System.Runtime.InteropServices;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Asks macOS which app opens web links - <c>NSWorkspace.URLForApplicationToOpenURL:</c> - so a
/// profile set to "Default browser" can still find, focus and close its tabs when that browser is
/// one ContextSwitcher can script. Most profiles are: it is what a new one starts with.
/// </summary>
public sealed class MacDefaultBrowser : IDefaultBrowser
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";

    /// <inheritdoc />
    public DefaultBrowser? Get()
    {
        if (!OperatingSystem.IsMacOS() || !NativeLibrary.TryLoad(AppKit, out _))
        {
            return null;
        }

        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            IntPtr workspace = Send(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace"));
            IntPtr address = SendText(objc_getClass("NSString"), sel_registerName("stringWithUTF8String:"), "https://example.com/");
            IntPtr link = Send(objc_getClass("NSURL"), sel_registerName("URLWithString:"), address);
            IntPtr app = workspace == IntPtr.Zero || link == IntPtr.Zero
                ? IntPtr.Zero
                : Send(workspace, sel_registerName("URLForApplicationToOpenURL:"), link);
            if (app == IntPtr.Zero)
            {
                return null;
            }

            IntPtr bundle = Send(objc_getClass("NSBundle"), sel_registerName("bundleWithURL:"), app);
            string? identifier = bundle == IntPtr.Zero ? null : Text(Send(bundle, sel_registerName("bundleIdentifier")));
            string name = Path.GetFileNameWithoutExtension(Text(Send(app, sel_registerName("path"))) ?? string.Empty);
            return new DefaultBrowser(KindOf(identifier), name);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            AppLog.MissingApi("the default browser lookup", ex);
            return null;
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    /// <summary>The browser a bundle identifier belongs to, among those ContextSwitcher can script.</summary>
    public static BrowserKind KindOf(string? bundleIdentifier) => bundleIdentifier?.ToLowerInvariant() switch
    {
        "com.google.chrome" => BrowserKind.Chrome,
        "com.brave.browser" => BrowserKind.Brave,
        "com.apple.safari" => BrowserKind.Safari,
        _ => BrowserKind.Default
    };

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
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendText(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);
}
