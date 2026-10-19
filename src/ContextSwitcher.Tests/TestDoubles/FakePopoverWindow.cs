using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>A popover window the system says nothing about, as off a Mac: it shows as an ordinary window.</summary>
public sealed class FakePopoverWindow : IPopoverWindow
{
    public bool Prepare(IntPtr window) => false;

    public string? Describe(IntPtr window) => null;

    public int? FrontmostProcessId() => null;

    public bool? IsMouseDown() => null;

    public bool? IsPointerOutside(IntPtr window, double left, double top, double right, double bottom) => null;
}
