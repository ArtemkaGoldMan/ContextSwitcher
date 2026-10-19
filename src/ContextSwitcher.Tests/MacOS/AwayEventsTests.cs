using System.Runtime.InteropServices;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

public sealed class AwayEventsTests
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";

    /// <summary>
    /// The notifications macOS posts on sleep and wake and on switching users, posted here in this
    /// process only, reach the events - through the real NSWorkspace notification center and the
    /// observer class made at runtime. The lock screen's are distributed to every app on the Mac, so
    /// they aren't posted from a test.
    /// </summary>
    [MacOSFact]
    public void SleepWakeAndUserSwitchingFromMacOSReachTheEvents()
    {
        AwayEvents events = new();
        Assert.True(events.IsListening);
        List<string> heard = [];
        events.Away += (_, reason) => heard.Add($"away {reason}");
        events.Back += (_, reason) => heard.Add($"back {reason}");

        Post("NSWorkspaceWillSleepNotification");
        Post("NSWorkspaceDidWakeNotification");
        Post("NSWorkspaceSessionDidResignActiveNotification");
        Post("NSWorkspaceSessionDidBecomeActiveNotification");

        Assert.Equal(["away Sleep", "back Sleep", "away OtherUser", "back OtherUser"], heard);
    }

    private static void Post(string symbol)
    {
        IntPtr appKit = NativeLibrary.Load(AppKit);
        IntPtr name = Marshal.ReadIntPtr(NativeLibrary.GetExport(appKit, symbol));
        IntPtr center = Send(Send(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace")), sel_registerName("notificationCenter"));
        SendPost(center, sel_registerName("postNotificationName:object:"), name, IntPtr.Zero);
    }

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern void SendPost(IntPtr receiver, IntPtr selector, IntPtr name, IntPtr sender);
}
