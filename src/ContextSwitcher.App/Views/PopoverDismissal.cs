namespace ContextSwitcher.App.Views;

/// <summary>
/// When the menu bar popover closes because the person clicked somewhere else: another app came to
/// the front, or a mouse button went down outside its card. Kept apart from the window, and from what
/// the system is asked, so it can be tested.
/// </summary>
public static class PopoverDismissal
{
    /// <summary>
    /// Whether a popover opened while <paramref name="baseline"/> was frontmost should now close, and
    /// the baseline to carry forward.
    ///
    /// Our own app coming forward - a click inside the popover - moves the baseline to us rather than
    /// closing it; any other app coming forward means the user clicked somewhere else. An app that was
    /// already frontmost when the popover opened does not count, since nothing changed.
    /// </summary>
    public static (bool Dismiss, int? Baseline) AfterFrontmostLook(int? baseline, int? now, int self)
    {
        if (now is null)
        {
            return (false, baseline);
        }

        // Nothing to compare against yet: the first reading becomes the reference point.
        if (baseline is null)
        {
            return (false, now);
        }

        if (now == self)
        {
            return (false, self);
        }

        return (now != baseline, baseline);
    }

    /// <summary>
    /// Whether this reading is a fresh press outside the card: the button has just gone down - it was
    /// up at the last reading - with the pointer outside. A press that began inside and is dragged out
    /// does not count, and neither does a button already held when the popover opened.
    /// </summary>
    public static bool IsPressOutside(bool wasDown, bool isDown, bool pointerOutside) =>
        isDown && !wasDown && pointerOutside;
}
