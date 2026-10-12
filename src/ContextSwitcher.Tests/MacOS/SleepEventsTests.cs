using System.Runtime.InteropServices;
using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

public sealed class SleepEventsTests
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";

    /// <summary>
    /// The notifications macOS posts on sleep and wake, posted here in this process only, reach the
    /// events - through the real NSWorkspace notification center and the observer class made at runtime.
    /// </summary>
    [Fact]
    public void SleepAndWakeFromMacOSReachTheEvents()
    {
        SleepEvents events = new();
        Assert.True(events.IsListening);
        List<string> heard = [];
        events.WillSleep += (_, _) => heard.Add("sleep");
        events.DidWake += (_, _) => heard.Add("wake");

        Post("NSWorkspaceWillSleepNotification");
        Post("NSWorkspaceDidWakeNotification");

        Assert.Equal(["sleep", "wake"], heard);
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
