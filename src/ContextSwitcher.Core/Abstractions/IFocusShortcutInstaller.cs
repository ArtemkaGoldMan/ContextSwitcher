namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Writes the Shortcut that turns a built-in Focus on, or the one that turns it off, and hands it to
/// the Shortcuts app, which asks the user to add it. Apps cannot switch Focus themselves, and cannot
/// add a Shortcut without that confirmation, so this is the fewest steps there are.
/// </summary>
public interface IFocusShortcutInstaller
{
    /// <summary>
    /// Offers the Shortcut that turns the built-in <paramref name="modeName"/> on, or off when
    /// <paramref name="turnOff"/> is set. Returns null once the Shortcuts app has it, or why it
    /// could not be offered.
    /// </summary>
    Task<string?> OfferAsync(string modeName, bool turnOff, CancellationToken cancellationToken);
}
