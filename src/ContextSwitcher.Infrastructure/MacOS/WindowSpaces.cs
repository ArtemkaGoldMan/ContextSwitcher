using System.Runtime.InteropServices;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Which desktop (Space) a window appears on - its AppKit <c>collectionBehavior</c>. By default a
/// window belongs to the Space it was first shown on, and showing it again puts it back there: the
/// menu bar popover opened on a desktop the user wasn't looking at, so "Open Dashboard" seemed to do
/// nothing.
/// </summary>
public static class WindowSpaces
{
    /// <summary><c>NSWindowCollectionBehaviorCanJoinAllSpaces</c>: on every Space at once.</summary>
    public const nuint CanJoinAllSpaces = 1 << 0;

    /// <summary><c>NSWindowCollectionBehaviorMoveToActiveSpace</c>: moves to the Space in front whenever it is shown.</summary>
    public const nuint MoveToActiveSpace = 1 << 1;

    /// <summary><c>NSWindowCollectionBehaviorFullScreenPrimary</c>: can itself go full screen.</summary>
    public const nuint FullScreenPrimary = 1 << 7;

    /// <summary><c>NSWindowCollectionBehaviorFullScreenAuxiliary</c>: may show over another app in full screen.</summary>
    public const nuint FullScreenAuxiliary = 1 << 8;

    /// <summary><c>NSWindowCollectionBehaviorFullScreenNone</c>: has nothing to do with full screen.</summary>
    public const nuint FullScreenNone = 1 << 9;

    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    /// <summary>
    /// A window's behaviour once it follows the user: whatever it had, plus showing on the Space in
    /// front and over full-screen apps. AppKit throws on the pairs that contradict those - on all
    /// Spaces at once, or full screen itself (which every Avalonia window starts with) - so they go.
    /// </summary>
    public static nuint FollowingTheActiveSpace(nuint current) =>
        (current & ~(CanJoinAllSpaces | FullScreenPrimary | FullScreenNone)) | MoveToActiveSpace | FullScreenAuxiliary;

    /// <summary>Makes <paramref name="nsWindow"/> appear on whichever Space is in front when it is shown.</summary>
    public static void FollowActiveSpace(IntPtr nsWindow)
    {
        if (nsWindow == IntPtr.Zero || !OperatingSystem.IsMacOS())
        {
            return;
        }

        nuint current = SendReturningNUInt(nsWindow, sel_registerName("collectionBehavior"));
        SendNUInt(nsWindow, sel_registerName("setCollectionBehavior:"), FollowingTheActiveSpace(current));
    }

    /// <summary>A window's behaviour and whether it is on the Space in front, for the log; null off a Mac.</summary>
    public static (nuint Behavior, bool OnActiveSpace)? Describe(IntPtr nsWindow)
    {
        if (nsWindow == IntPtr.Zero || !OperatingSystem.IsMacOS())
        {
            return null;
        }

        return (
            SendReturningNUInt(nsWindow, sel_registerName("collectionBehavior")),
            SendReturningByte(nsWindow, sel_registerName("isOnActiveSpace")) != 0);
    }

    [DllImport(ObjectiveC)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern nuint SendReturningNUInt(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern byte SendReturningByte(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern void SendNUInt(IntPtr receiver, IntPtr selector, nuint value);
}
