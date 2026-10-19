using System.Runtime.InteropServices;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// The apps running with a place in the Dock, from <c>NSWorkspace.runningApplications</c>: those whose
/// activation policy is "regular". Menu bar helpers and background agents aren't, so they never
/// appear; Finder is, but is always running and not the user's to close, and ContextSwitcher is the
/// one asking - both are left out, and so is any other copy of ContextSwitcher, such as one run from
/// the code beside the installed one. Each is named as Finder shows its <c>.app</c>, which is how a
/// profile lists apps.
/// </summary>
public sealed class MacRunningApplications : IRunningApplications
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";

    /// <summary><c>NSApplicationActivationPolicyRegular</c>: an app in the Dock, with windows.</summary>
    private const long Regular = 0;

    private static readonly string[] NeverListed = ["com.apple.finder", "com.artem.contextswitcher"];

    /// <inheritdoc />
    public IReadOnlyList<RunningApp> List()
    {
        if (!OperatingSystem.IsMacOS() || !NativeLibrary.TryLoad(AppKit, out _))
        {
            return [];
        }

        List<RunningApp> apps = [];
        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            IntPtr running = Send(Send(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace")), sel_registerName("runningApplications"));
            nuint count = running == IntPtr.Zero ? 0 : SendReturningNUInt(running, sel_registerName("count"));
            for (nuint i = 0; i < count; i++)
            {
                IntPtr app = SendWithIndex(running, sel_registerName("objectAtIndex:"), i);
                if (SendReturningLong(app, sel_registerName("activationPolicy")) != Regular
                    || SendReturningByte(app, sel_registerName("isTerminated")) != 0
                    || SendReturningInt(app, sel_registerName("processIdentifier")) == Environment.ProcessId
                    || NeverListed.Contains(Text(Send(app, sel_registerName("bundleIdentifier"))), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? path = Text(Send(Send(app, sel_registerName("bundleURL")), sel_registerName("path")));
                string? localizedName = Text(Send(app, sel_registerName("localizedName")));
                string? fileName = path is null ? null : Path.GetFileNameWithoutExtension(path);
                string? name = fileName ?? localizedName;
                if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "ContextSwitcher", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                apps.Add(new RunningApp(name, BundleName(path), localizedName));
            }
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }

        return apps;
    }

    /// <summary>The <c>CFBundleName</c> in the app's Info.plist - "Code" for Visual Studio Code.</summary>
    private static string? BundleName(string? path)
    {
        if (path is null)
        {
            return null;
        }

        IntPtr bundle = SendText(objc_getClass("NSBundle"), sel_registerName("bundleWithPath:"), Text(path));
        return bundle == IntPtr.Zero ? null : Text(SendWithObject(bundle, sel_registerName("objectForInfoDictionaryKey:"), Text("CFBundleName")));
    }

    private static IntPtr Text(string text) =>
        SendUtf8(objc_getClass("NSString"), sel_registerName("stringWithUTF8String:"), text);

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
    private static extern IntPtr SendWithObject(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendText(IntPtr receiver, IntPtr selector, IntPtr text);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendUtf8(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern nuint SendReturningNUInt(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern long SendReturningLong(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern int SendReturningInt(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern byte SendReturningByte(IntPtr receiver, IntPtr selector);
}
