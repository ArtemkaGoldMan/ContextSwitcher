using System.Windows.Input;
using Avalonia.Media;
using ContextSwitcher.App.Resources;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One row on the Profiles page. Clicking the profile turns it on - or, when it is the one on, off,
/// as its button in the menu bar popover does; there is no separate Activate button. The profile on
/// is plain to see: its card washed in its own colour, edged in it, and its name in bold. Edit,
/// Duplicate, and Delete are kept as separate actions so "switch to this" and "edit this" can never
/// be triggered by the same click (agent.md section 11.1.2). Delete requires a second click to
/// confirm rather than a modal dialog.
/// </summary>
public sealed class ProfileRowViewModel : ViewModelBase
{
    private bool isConfirmingDelete;

    public ProfileRowViewModel(
        ContextDefinition context,
        bool isActive,
        Func<ContextDefinition, Task> toggle,
        Action<ContextDefinition> edit,
        Func<ContextDefinition, Task> duplicate,
        Func<ContextDefinition, Task> delete)
    {
        this.Context = context;
        this.IsActive = isActive;
        this.AccentBrush = AccentColorParser.ToBrush(context.AccentColor);
        this.CardTint = isActive ? AccentColorParser.ToTint(context.AccentColor, 0x1F) : Brushes.Transparent;
        this.Icon = context.Icon;

        this.ToggleCommand = new AsyncRelayCommand(() => toggle(context));
        this.EditCommand = new RelayCommand(() => edit(context));
        this.DuplicateCommand = new AsyncRelayCommand(() => duplicate(context));
        this.DeleteCommand = new RelayCommand(() =>
        {
            if (this.IsConfirmingDelete)
            {
                this.IsConfirmingDelete = false;
                _ = delete(context);
            }
            else
            {
                this.IsConfirmingDelete = true;
            }
        });
        this.CancelDeleteCommand = new RelayCommand(() => this.IsConfirmingDelete = false);
    }

    public ContextDefinition Context { get; }

    public string DisplayName => this.Context.DisplayName;

    /// <summary>The stored icon name, drawn by ProfileIconConverter.</summary>
    public string Icon { get; }

    public IBrush AccentBrush { get; }

    public bool IsActive { get; }

    /// <summary>A wash of the profile's colour over the card while it is on; nothing otherwise.</summary>
    public IBrush CardTint { get; }

    public FontWeight NameWeight => this.IsActive ? FontWeight.Bold : FontWeight.Medium;

    public string ToggleToolTip => this.IsActive ? Strings.Dashboard_ClickToTurnOff : Strings.Profiles_ClickToTurnOn;

    public bool IsConfirmingDelete
    {
        get => this.isConfirmingDelete;
        private set => this.SetProperty(ref this.isConfirmingDelete, value);
    }

    /// <summary>Turns the profile on, or off when it is the one on.</summary>
    public ICommand ToggleCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand DuplicateCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand CancelDeleteCommand { get; }
}
