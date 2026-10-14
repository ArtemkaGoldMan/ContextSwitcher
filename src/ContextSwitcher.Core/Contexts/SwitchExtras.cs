using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// What a switch closes besides what its profiles say: the apps and tabs the user chose to close when
/// asked about everything else open (<see cref="AppConfiguration.AskOnSwitch"/>).
/// </summary>
public sealed record SwitchExtras(IReadOnlyList<string> CloseApps, IReadOnlyList<TabToClose> CloseTabs)
{
    /// <summary>Nothing besides the profiles.</summary>
    public static SwitchExtras None { get; } = new([], []);

    /// <summary>Whether there is nothing extra to close.</summary>
    public bool IsEmpty => this.CloseApps.Count == 0 && this.CloseTabs.Count == 0;
}

/// <summary>A tab to close, by the browser it is in and its address.</summary>
public sealed record TabToClose(BrowserKind Browser, string Url);
