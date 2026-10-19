namespace ContextSwitcher.Tests.Windows;

/// <summary>
/// A test of how Windows behaves - refusing to move a file another process holds open, say - that has
/// nothing to ask anywhere else, so it is skipped there and says why.
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            this.Skip = "Needs Windows.";
        }
    }
}
