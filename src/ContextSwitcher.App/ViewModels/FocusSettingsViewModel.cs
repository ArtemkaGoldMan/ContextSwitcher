using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Profile Setup's Focus section: one dropdown instead of a toggle plus a name to type.
///
/// A switch turns Focus on by running a Shortcut named <c>ContextSwitcher - Focus &lt;mode&gt;</c>
/// (docs/shortcuts-integration.md), so the mode name only works if that Shortcut exists - and a
/// missing one cost about eight seconds per switch before it failed. The dropdown therefore lists
/// the modes whose Shortcut is already there first, marks the rest, and says exactly which
/// Shortcut is still missing for the one chosen.
/// </summary>
public sealed class FocusSettingsViewModel : ViewModelBase
{
    /// <summary>What every Focus Shortcut's name starts with.</summary>
    public const string ShortcutPrefix = FocusModes.ShortcutPrefix;


    /// <summary>How long to keep checking for a Shortcut the user was just offered.</summary>
    private static readonly TimeSpan WaitForAdd = TimeSpan.FromMinutes(2);

    private readonly ISystemCatalog catalog;
    private readonly IProcessRunner processRunner;
    private readonly IFocusShortcutInstaller installer;
    private string shortcutStatus = string.Empty;
    private bool isOffering;

    private HashSet<string> shortcutNames = new(StringComparer.Ordinal);
    private HashSet<string> shortcutModes = new(StringComparer.Ordinal);
    private HashSet<string> offShortcutModes = new(StringComparer.Ordinal);
    private bool legacyOffExists;
    private bool shortcutsLoaded;
    private IReadOnlyList<FocusChoiceViewModel> choices = [];
    private FocusChoiceViewModel selected;

    public FocusSettingsViewModel(ISystemCatalog catalog, IProcessRunner processRunner, IFocusShortcutInstaller installer, FocusConfig focus)
    {
        this.catalog = catalog;
        this.processRunner = processRunner;
        this.installer = installer;

        string current = focus.Enabled ? focus.ModeName.Trim() : string.Empty;
        this.selected = this.Rebuild(current);

        this.OpenShortcutsCommand = new RelayCommand(this.OpenShortcuts);
        this.RecheckCommand = new RelayCommand(() => _ = this.LoadShortcutsAsync());
        this.CreateShortcutCommand = new RelayCommand(() => _ = this.OfferShortcutAsync(this.Selected.ModeName, turnOff: false), () => !this.isOffering);
        this.CreateOffShortcutCommand = new RelayCommand(() => _ = this.OfferShortcutAsync(this.Selected.ModeName, turnOff: true), () => !this.isOffering);

        _ = this.LoadShortcutsAsync();
    }

    public IReadOnlyList<FocusChoiceViewModel> Choices
    {
        get => this.choices;
        private set => this.SetProperty(ref this.choices, value);
    }

    public FocusChoiceViewModel Selected
    {
        get => this.selected;
        set
        {
            // A ComboBox writes null while its items are being swapped; that is not a choice.
            if (value is not null && this.SetProperty(ref this.selected, value))
            {
                this.OnHintsChanged();
            }
        }
    }

    public bool IsEnabled => !this.Selected.IsNone;

    /// <summary>The Shortcut the chosen mode needs, for the hint under the dropdown.</summary>
    public string MissingShortcutText =>
        $"Needs a Shortcut named “{ShortcutPrefix}{this.Selected.ModeName}” with a Set Focus action that turns this Focus on.";

    public bool IsShortcutMissing => this.shortcutsLoaded && this.IsEnabled && !this.Selected.HasShortcut;

    /// <summary>
    /// Leaving this profile for one without Focus runs this mode's Off Shortcut; without it, Focus
    /// stays on. A Focus the user made can fall back on the original single "Focus Off" they built
    /// by hand; a built-in one is always offered its own, which cannot trip over other modes.
    /// </summary>
    public bool IsOffShortcutMissing =>
        this.shortcutsLoaded
        && this.IsEnabled
        && !this.offShortcutModes.Contains(this.Selected.ModeName)
        && (FocusModes.Find(this.Selected.ModeName) is not null || !this.legacyOffExists);

    public string MissingOffShortcutText =>
        $"Also add “{FocusModes.OffShortcutName(this.Selected.ModeName)}” so {this.Selected.ModeName} turns off when you switch to a profile without Focus.";

    public bool HasHint => this.IsShortcutMissing || this.IsOffShortcutMissing;

    /// <summary>
    /// The missing Shortcut is for a mode macOS ships with, so Context Switcher can write it. A mode
    /// the user made has an identifier no other app can see, and still has to be set up by hand.
    /// </summary>
    public bool CanCreateShortcut => this.IsShortcutMissing && FocusModes.Find(this.Selected.ModeName) is not null;

    public bool CanCreateOffShortcut => this.IsOffShortcutMissing && FocusModes.Find(this.Selected.ModeName) is not null;

    /// <summary>What happened to the last Shortcut offered: waiting for the user to add it, or why it failed.</summary>
    public string ShortcutStatus
    {
        get => this.shortcutStatus;
        private set
        {
            if (this.SetProperty(ref this.shortcutStatus, value))
            {
                this.OnPropertyChanged(nameof(this.HasShortcutStatus));
            }
        }
    }

    public bool HasShortcutStatus => this.ShortcutStatus.Length > 0;

    /// <summary>Writes the chosen mode's Shortcut and opens it in Shortcuts, which asks to add it.</summary>
    public RelayCommand CreateShortcutCommand { get; }

    /// <summary>The same for the Shortcut that turns Focus off.</summary>
    public RelayCommand CreateOffShortcutCommand { get; }

    /// <summary>How often to look for the Shortcut while waiting for the user to add it.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    public RelayCommand OpenShortcutsCommand { get; }

    /// <summary>Re-reads the Shortcuts, after the user has gone and made the missing one.</summary>
    public RelayCommand RecheckCommand { get; }

    public FocusConfig ToConfig() => new()
    {
        Enabled = this.IsEnabled,
        ModeName = this.Selected.ModeName
    };

    /// <summary>Reads which Focus Shortcuts exist. Never throws; on failure nothing is marked ready.</summary>
    public async Task LoadShortcutsAsync()
    {
        IReadOnlyList<string> names;
        try
        {
            names = await this.catalog.GetShortcutNamesAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception)
        {
            names = [];
        }

        this.shortcutNames = names.ToHashSet(StringComparer.Ordinal);
        this.shortcutModes = new HashSet<string>(StringComparer.Ordinal);
        this.offShortcutModes = new HashSet<string>(StringComparer.Ordinal);
        this.legacyOffExists = false;
        foreach (string name in names)
        {
            if (!FocusModes.TryParseShortcutName(name, out string mode, out bool turnsOff))
            {
                continue;
            }

            if (!turnsOff)
            {
                this.shortcutModes.Add(mode);
            }
            else if (mode.Length > 0)
            {
                this.offShortcutModes.Add(mode);
            }
            else
            {
                this.legacyOffExists = true;
            }
        }

        this.shortcutsLoaded = true;

        // Raised even when the entry is the same object (None is shared): swapping Choices makes a
        // ComboBox drop its selection, and only this notification puts it back.
        this.selected = this.Rebuild(this.Selected.ModeName);
        this.OnPropertyChanged(nameof(this.Selected));
        this.OnHintsChanged();
    }

    /// <summary>
    /// Offers a Shortcut, then watches for it to appear - the Shortcuts app's "Add Shortcut" is the
    /// user's click, not ours - and stops as soon as it does, so the status turns to Ready without
    /// anyone pressing "Check again". Never throws.
    /// </summary>
    public async Task OfferShortcutAsync(string modeName, bool turnOff)
    {
        string expected = turnOff ? FocusModes.OffShortcutName(modeName) : FocusModes.ShortcutName(modeName);
        this.SetOffering(true);
        try
        {
            string? problem = await this.installer.OfferAsync(modeName, turnOff, CancellationToken.None).ConfigureAwait(true);
            if (problem is not null)
            {
                this.ShortcutStatus = problem;
                return;
            }

            this.ShortcutStatus = "Click “Add Shortcut” in the Shortcuts window that opened.";
            DateTime deadline = DateTime.UtcNow + WaitForAdd;
            while (DateTime.UtcNow < deadline)
            {
                await this.LoadShortcutsAsync().ConfigureAwait(true);
                if (this.shortcutNames.Contains(expected))
                {
                    this.ShortcutStatus = string.Empty;
                    return;
                }

                await Task.Delay(this.PollInterval).ConfigureAwait(true);
            }

            this.ShortcutStatus = "The Shortcut wasn't added. Try again, or check the Shortcuts app.";
        }
        catch (Exception ex)
        {
            this.ShortcutStatus = $"Couldn't create the Shortcut: {ex.Message}";
        }
        finally
        {
            this.SetOffering(false);
        }
    }

    /// <summary>
    /// Lists None, then every mode with a Shortcut, then the built-in modes still without one -
    /// plus the current mode wherever it falls, so a name from config is never silently dropped.
    /// Returns the entry for <paramref name="current"/>.
    /// </summary>
    private FocusChoiceViewModel Rebuild(string current)
    {
        List<FocusChoiceViewModel> list = [FocusChoiceViewModel.None];
        IEnumerable<string> ready = this.shortcutModes.Order(StringComparer.CurrentCultureIgnoreCase);
        foreach (string mode in ready.Concat(FocusModes.BuiltIn.Select(builtIn => builtIn.Name)).Append(current))
        {
            if (mode.Length > 0 && !list.Any(choice => choice.ModeName == mode))
            {
                list.Add(new FocusChoiceViewModel(mode, this.shortcutModes.Contains(mode), this.shortcutsLoaded));
            }
        }

        this.Choices = list;
        return list.First(choice => choice.ModeName == current);
    }

    private void SetOffering(bool offering)
    {
        this.isOffering = offering;
        this.CreateShortcutCommand.RaiseCanExecuteChanged();
        this.CreateOffShortcutCommand.RaiseCanExecuteChanged();
    }

    private void OnHintsChanged()
    {
        this.OnPropertyChanged(nameof(this.MissingOffShortcutText));
        this.OnPropertyChanged(nameof(this.CanCreateShortcut));
        this.OnPropertyChanged(nameof(this.CanCreateOffShortcut));
        this.OnPropertyChanged(nameof(this.IsEnabled));
        this.OnPropertyChanged(nameof(this.IsShortcutMissing));
        this.OnPropertyChanged(nameof(this.IsOffShortcutMissing));
        this.OnPropertyChanged(nameof(this.MissingShortcutText));
        this.OnPropertyChanged(nameof(this.HasHint));
    }

    private void OpenShortcuts()
    {
        _ = this.processRunner.RunAsync(new ProcessStartOptions("open", ["-a", "Shortcuts"], TimeSpan.FromSeconds(5)), CancellationToken.None);
    }
}

/// <summary>One entry in the Focus dropdown.</summary>
public sealed class FocusChoiceViewModel
{
    public static readonly FocusChoiceViewModel None = new(string.Empty, hasShortcut: false, statusKnown: false);

    public FocusChoiceViewModel(string modeName, bool hasShortcut, bool statusKnown)
    {
        this.ModeName = modeName;
        this.HasShortcut = hasShortcut;
        this.Status = !statusKnown || this.IsNone ? string.Empty : hasShortcut ? "Ready" : "Needs Shortcut";
    }

    /// <summary>The mode name stored in config; empty for <see cref="None"/>.</summary>
    public string ModeName { get; }

    public bool IsNone => this.ModeName.Length == 0;

    public string Label => this.IsNone ? "Don't use Focus" : this.ModeName;

    public bool HasShortcut { get; }

    /// <summary>"Ready" or "Needs Shortcut", shown beside the name; empty until Shortcuts are read.</summary>
    public string Status { get; }

    public bool IsReady => this.HasShortcut && !this.IsNone;

    public override string ToString() => this.Label;
}
