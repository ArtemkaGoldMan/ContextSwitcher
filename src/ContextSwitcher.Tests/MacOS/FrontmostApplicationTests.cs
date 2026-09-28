using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

/// <summary>
/// The rule that closes the menu bar popover when the user clicks somewhere else.
/// </summary>
public sealed class FrontmostApplicationTests
{
    private const int Self = 100;
    private const int Editor = 200;
    private const int Finder = 300;

    [Fact]
    public void AnotherAppComingForwardDismissesThePopover()
    {
        Assert.Equal((true, Editor), FrontmostApplication.Evaluate(baseline: Editor, now: Finder, Self));
    }

    [Fact]
    public void NothingChangingKeepsItOpen()
    {
        Assert.Equal((false, Editor), FrontmostApplication.Evaluate(baseline: Editor, now: Editor, Self));
    }

    /// <summary>
    /// Clicking inside the popover brings this app forward. That must not close it - it moves the
    /// baseline to us, so the next app to come forward is what counts.
    /// </summary>
    [Fact]
    public void AClickInsideThePopoverKeepsItOpenAndMovesTheBaselineToUs()
    {
        (bool dismiss, int? baseline) = FrontmostApplication.Evaluate(baseline: Editor, now: Self, Self);
        Assert.False(dismiss);
        Assert.Equal(Self, baseline);

        Assert.True(FrontmostApplication.Evaluate(baseline, now: Editor, Self).Dismiss);
    }

    [Fact]
    public void AnUnreadableFrontmostAppChangesNothing()
    {
        Assert.Equal((false, Editor), FrontmostApplication.Evaluate(baseline: Editor, now: null, Self));
    }

    /// <summary>If the reading failed when the popover opened, the first good one is the reference, not a change.</summary>
    [Fact]
    public void WithNoBaselineTheFirstReadingIsAdoptedRatherThanTreatedAsAChange()
    {
        Assert.Equal((false, Finder), FrontmostApplication.Evaluate(baseline: null, now: Finder, Self));
    }
}
