using ContextSwitcher.App.Views;

namespace ContextSwitcher.Tests.App.Views;

/// <summary>
/// The rule that closes the menu bar popover when the user clicks somewhere else.
/// </summary>
public sealed class PopoverDismissalTests
{
    private const int Self = 100;
    private const int Editor = 200;
    private const int Finder = 300;

    [Fact]
    public void AnotherAppComingForwardDismissesThePopover()
    {
        Assert.Equal((true, Editor), PopoverDismissal.AfterFrontmostLook(baseline: Editor, now: Finder, Self));
    }

    [Fact]
    public void NothingChangingKeepsItOpen()
    {
        Assert.Equal((false, Editor), PopoverDismissal.AfterFrontmostLook(baseline: Editor, now: Editor, Self));
    }

    /// <summary>
    /// Clicking inside the popover brings this app forward. That must not close it - it moves the
    /// baseline to us, so the next app to come forward is what counts.
    /// </summary>
    [Fact]
    public void AClickInsideThePopoverKeepsItOpenAndMovesTheBaselineToUs()
    {
        (bool dismiss, int? baseline) = PopoverDismissal.AfterFrontmostLook(baseline: Editor, now: Self, Self);
        Assert.False(dismiss);
        Assert.Equal(Self, baseline);

        Assert.True(PopoverDismissal.AfterFrontmostLook(baseline, now: Editor, Self).Dismiss);
    }

    [Fact]
    public void AnUnreadableFrontmostAppChangesNothing()
    {
        Assert.Equal((false, Editor), PopoverDismissal.AfterFrontmostLook(baseline: Editor, now: null, Self));
    }

    /// <summary>If the reading failed when the popover opened, the first good one is the reference, not a change.</summary>
    [Fact]
    public void WithNoBaselineTheFirstReadingIsAdoptedRatherThanTreatedAsAChange()
    {
        Assert.Equal((false, Finder), PopoverDismissal.AfterFrontmostLook(baseline: null, now: Finder, Self));
    }

    [Fact]
    public void AFreshPressOutsideTheCardCounts()
    {
        Assert.True(PopoverDismissal.IsPressOutside(wasDown: false, isDown: true, pointerOutside: true));
    }

    [Theory]
    [InlineData(false, true, false)] // a press inside the card: a button being clicked
    [InlineData(true, true, true)]   // still held from before - a drag, or the click that opened it
    [InlineData(false, false, true)] // no button down at all
    [InlineData(true, false, true)]  // a release
    public void AnythingElseDoesNot(bool wasDown, bool isDown, bool pointerOutside)
    {
        Assert.False(PopoverDismissal.IsPressOutside(wasDown, isDown, pointerOutside));
    }
}
