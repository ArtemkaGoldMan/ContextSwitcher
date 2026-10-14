using System.Runtime.InteropServices;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Whether there is a display awake to draw on. Avalonia paces its drawing with a display link, and
/// macOS will not start one while every display is asleep, so an app launched then - from a Shortcut,
/// a script, or right after a restart onto a dark screen - aborted before showing anything, with
/// "Avalonia.Native was not able to start the RenderTimer. Native error code is: -6661"
/// (<c>kCVReturnInvalidDisplay</c>), and macOS offered the user a crash report.
/// </summary>
public static class DisplayAvailability
{
    /// <summary>How often a launch that finds every display asleep looks again: soon enough that the app is up when the screen wakes.</summary>
    public static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(3);

    private const string CoreVideo = "/System/Library/Frameworks/CoreVideo.framework/CoreVideo";

    private delegate int OutputCallback(IntPtr displayLink, IntPtr now, IntPtr outputTime, ulong flagsIn, IntPtr flagsOut, IntPtr context);

    // Starting a display link needs a callback to call; this one draws nothing. Held in a field so the
    // runtime cannot collect it while native code holds its address.
    private static readonly OutputCallback NoFrames = (_, _, _, _, _, _) => 0;

    /// <summary>
    /// Whether a display link can be started right now - the very thing Avalonia is about to do, asked
    /// the same way, so the answer cannot disagree with it. <see langword="true"/> where it cannot be
    /// asked (not macOS, or CoreVideo missing, as in a test host): not knowing is no reason to wait.
    /// </summary>
    public static bool CanDraw()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return true;
        }

        try
        {
            if (CVDisplayLinkCreateWithActiveCGDisplays(out IntPtr link) != 0 || link == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return CVDisplayLinkSetOutputCallback(link, NoFrames, IntPtr.Zero) == 0
                    && CVDisplayLinkStart(link) == 0;
            }
            finally
            {
                _ = CVDisplayLinkStop(link);
                CVDisplayLinkRelease(link);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return true;
        }
    }

    /// <summary>Whether <paramref name="exception"/> is Avalonia failing to start its render timer - what a launch with no display awake ends in.</summary>
    public static bool IsRenderTimerFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is InvalidOperationException
            && exception.Message.Contains("RenderTimer", StringComparison.Ordinal);
    }

    /// <summary>Returns once a display is awake, having waited that long: zero when one already was.</summary>
    public static TimeSpan WaitUntilAvailable() => WaitUntilAvailable(CanDraw, PollEvery, Thread.Sleep);

    /// <summary>
    /// Polls <paramref name="canDraw"/> every <paramref name="pollEvery"/> until it says yes; the wait is
    /// the caller's to make, so a test need not sleep. Nothing else runs meanwhile, so the app costs a
    /// look every few seconds and no more while the screen is dark.
    /// </summary>
    public static TimeSpan WaitUntilAvailable(Func<bool> canDraw, TimeSpan pollEvery, Action<TimeSpan> pause)
    {
        ArgumentNullException.ThrowIfNull(canDraw);
        ArgumentNullException.ThrowIfNull(pause);

        TimeSpan waited = TimeSpan.Zero;
        while (!canDraw())
        {
            pause(pollEvery);
            waited += pollEvery;
        }

        return waited;
    }

    [DllImport(CoreVideo)]
    private static extern int CVDisplayLinkCreateWithActiveCGDisplays(out IntPtr displayLink);

    [DllImport(CoreVideo)]
    private static extern int CVDisplayLinkSetOutputCallback(IntPtr displayLink, OutputCallback callback, IntPtr userInfo);

    [DllImport(CoreVideo)]
    private static extern int CVDisplayLinkStart(IntPtr displayLink);

    [DllImport(CoreVideo)]
    private static extern int CVDisplayLinkStop(IntPtr displayLink);

    [DllImport(CoreVideo)]
    private static extern void CVDisplayLinkRelease(IntPtr displayLink);
}
