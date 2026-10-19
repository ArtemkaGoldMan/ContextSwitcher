using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

/// <summary>
/// The popover's window is only made in the running app; what a test can check is that a window not
/// yet made - no native handle - is left alone, and that nothing is claimed about it.
/// </summary>
public sealed class MacPopoverWindowTests
{
    [Fact]
    public void WithoutAWindowNothingIsDoneOrTold()
    {
        MacPopoverWindow popover = new();

        Assert.False(popover.Prepare(IntPtr.Zero));
        Assert.Null(popover.Describe(IntPtr.Zero));
        Assert.Null(popover.IsPointerOutside(IntPtr.Zero, 16, 10, 16, 22));
    }
}
