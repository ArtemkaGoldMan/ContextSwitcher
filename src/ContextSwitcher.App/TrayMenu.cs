using Avalonia.Controls;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App;

/// <summary>
/// The menu bar icon's menu: every profile first, with the active one ticked, so switching takes a
/// single click from the menu bar; then Open Dashboard and Quit.
///
/// One menu object lives for the whole run and is updated in place. On macOS, handing the tray icon
/// a replacement NativeMenu works once and then throws "The menu being updated does not match" from
/// Avalonia.Native's exporter, which stays bound to the menu it first exported - measured in the real
/// app, where the second switch crashed it. Headless tests cannot see that, since they have no tray.
///
/// Items use commands rather than Click handlers so the menu can be exercised in tests - there is no
/// way to click a real menu bar item from a test run.
/// </summary>
public sealed class TrayMenu
{
    public const string OpenDashboardLabel = "Open Dashboard";

    public const string QuitLabel = "Quit";

    private readonly Action<string> switchTo;
    private readonly List<(string ContextId, NativeMenuItem Item)> profileItems = [];
    private readonly NativeMenuItemSeparator profileSeparator = new();

    public TrayMenu(Action<string> switchTo, Action openDashboard, Action quit)
    {
        this.switchTo = switchTo;
        this.Menu = new NativeMenu();
        this.Menu.Items.Add(new NativeMenuItem(OpenDashboardLabel) { Command = new RelayCommand(openDashboard) });
        this.Menu.Items.Add(new NativeMenuItemSeparator());
        this.Menu.Items.Add(new NativeMenuItem(QuitLabel) { Command = new RelayCommand(quit) });
    }

    /// <summary>The single menu to hand the tray icon, once.</summary>
    public NativeMenu Menu { get; }

    /// <summary>
    /// Brings the profile entries in line with <paramref name="contexts"/> and ticks the active one.
    /// When the profiles themselves are unchanged - the common case, a switch - only the ticks and
    /// names are touched; otherwise the profile entries are replaced, still inside the same menu.
    /// </summary>
    public void Update(IReadOnlyList<ContextDefinition> contexts, string? currentContextId)
    {
        bool sameProfiles = contexts.Select(c => c.Id).SequenceEqual(this.profileItems.Select(p => p.ContextId));
        if (!sameProfiles)
        {
            foreach ((_, NativeMenuItem item) in this.profileItems)
            {
                this.Menu.Items.Remove(item);
            }

            this.Menu.Items.Remove(this.profileSeparator);

            this.profileItems.Clear();
            for (int i = 0; i < contexts.Count; i++)
            {
                string contextId = contexts[i].Id;
                NativeMenuItem item = new(contexts[i].DisplayName)
                {
                    ToggleType = MenuItemToggleType.Radio,
                    Command = new RelayCommand(() => this.switchTo(contextId))
                };
                this.Menu.Items.Insert(i, item);
                this.profileItems.Add((contextId, item));
            }

            // The separator under the profiles only when there is something above it to separate.
            if (contexts.Count > 0)
            {
                this.Menu.Items.Insert(contexts.Count, this.profileSeparator);
            }
        }

        for (int i = 0; i < contexts.Count; i++)
        {
            NativeMenuItem item = this.profileItems[i].Item;
            item.Header = contexts[i].DisplayName;
            item.IsChecked = contexts[i].Id == currentContextId;
        }
    }
}
