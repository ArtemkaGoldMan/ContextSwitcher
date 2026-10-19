namespace ContextSwitcher.Tests.MacOS;

/// <summary>
/// A test of macOS itself - <c>osascript</c>, AppKit, Launch Services - which has nothing to ask
/// anywhere else, so it is skipped there and says why.
/// </summary>
public sealed class MacOSFactAttribute : FactAttribute
{
    public MacOSFactAttribute()
    {
        if (!OperatingSystem.IsMacOS())
        {
            this.Skip = "Needs macOS.";
        }
    }
}
