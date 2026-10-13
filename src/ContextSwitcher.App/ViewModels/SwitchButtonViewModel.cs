using System.Windows.Input;
using Avalonia.Media;
using ContextSwitcher.App.Resources;

namespace ContextSwitcher.App.ViewModels;

public sealed class SwitchButtonViewModel : ViewModelBase
{
    private bool isCurrent;

    public SwitchButtonViewModel(string contextId, string displayName, string accentColorHex, string icon, ICommand switchCommand)
    {
        this.Icon = icon;
        this.ContextId = contextId;
        this.DisplayName = displayName;
        this.AccentBrush = AccentColorParser.ToBrush(accentColorHex);
        this.SwitchCommand = switchCommand;
    }

    public string ContextId { get; }

    public string DisplayName { get; }

    public IBrush AccentBrush { get; }

    /// <summary>The stored icon name, drawn by ProfileIconConverter.</summary>
    public string Icon { get; }

    public ICommand SwitchCommand { get; }

    public bool IsCurrent
    {
        get => this.isCurrent;
        set
        {
            if (this.SetProperty(ref this.isCurrent, value))
            {
                this.OnPropertyChanged(nameof(this.ToolTip));
            }
        }
    }

    /// <summary>
    /// Says what clicking does where it isn't obvious: the active profile's button turns it off,
    /// and nothing on screen said so.
    /// </summary>
    public string? ToolTip => this.IsCurrent ? Strings.Dashboard_ClickToTurnOff : null;
}
