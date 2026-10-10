using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>
/// Records what was offered. With <see cref="AddsTo"/> set it plays the user clicking "Add Shortcut"
/// as well: the Shortcut turns up in that catalog, the way it would in the real Shortcuts list.
/// </summary>
public sealed class FakeFocusShortcutInstaller : IFocusShortcutInstaller
{
    /// <summary>Each offer, as the name of the Shortcut it would add.</summary>
    public List<string> Offered { get; } = [];

    /// <summary>Returned instead of success, when a test wants the offer to fail.</summary>
    public string? Problem { get; set; }

    public FakeSystemCatalog? AddsTo { get; set; }

    public Task<string?> OfferAsync(string modeName, bool turnOff, CancellationToken cancellationToken)
    {
        string name = turnOff ? FocusModes.OffShortcutName(modeName) : FocusModes.ShortcutName(modeName);
        this.Offered.Add(name);
        if (this.Problem is null)
        {
            this.AddsTo?.ShortcutNames.Add(name);
        }

        return Task.FromResult(this.Problem);
    }
}
