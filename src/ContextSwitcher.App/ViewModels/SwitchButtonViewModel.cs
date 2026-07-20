using System.Windows.Input;
using Avalonia.Media;

namespace ContextSwitcher.App.ViewModels;

public sealed class SwitchButtonViewModel : ViewModelBase
{
    private bool isCurrent;

    public SwitchButtonViewModel(string contextId, string displayName, string accentColorHex, ICommand switchCommand)
    {
        this.ContextId = contextId;
        this.DisplayName = displayName;
        this.AccentBrush = AccentColorParser.ToBrush(accentColorHex);
        this.SwitchCommand = switchCommand;
    }

    public string ContextId { get; }

    public string DisplayName { get; }

    public IBrush AccentBrush { get; }

    public ICommand SwitchCommand { get; }

    public bool IsCurrent
    {
        get => this.isCurrent;
        set => this.SetProperty(ref this.isCurrent, value);
    }
}
