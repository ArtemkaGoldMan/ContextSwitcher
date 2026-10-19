namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Whether there is a display awake to draw on. A window can't start drawing while every display is
/// asleep, so a launch waits for one.
/// </summary>
public interface IDisplayAvailability
{
    /// <summary>Returns once a display is awake, having waited that long: zero when one already was.</summary>
    TimeSpan WaitUntilAvailable();

    /// <summary>Whether <paramref name="exception"/> is the window system failing to start drawing for want of a display.</summary>
    bool IsRenderTimerFailure(Exception exception);
}
