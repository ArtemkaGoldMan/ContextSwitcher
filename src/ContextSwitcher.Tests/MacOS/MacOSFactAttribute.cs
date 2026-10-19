namespace ContextSwitcher.Tests.MacOS;

/// <summary>
/// A test that needs macOS - <c>osascript</c>, AppKit, Launch Services, a home folder given through
/// <c>HOME</c> - and has nothing to ask anywhere else, so it is skipped there and says why.
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
