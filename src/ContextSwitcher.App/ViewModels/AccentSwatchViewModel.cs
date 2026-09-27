using Avalonia.Media;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One preset accent color in Profile Setup's swatch row.
/// </summary>
public sealed class AccentSwatchViewModel : ViewModelBase
{
    private bool isSelected;

    public AccentSwatchViewModel(string hex, string name, Action<string> select)
    {
        this.Hex = hex;
        this.Name = name;
        this.Brush = AccentColorParser.ToBrush(hex);
        this.SelectCommand = new RelayCommand(() => select(hex));
    }

    public string Hex { get; }

    /// <summary>Shown as a tooltip and read out by VoiceOver, since the swatch itself is only color.</summary>
    public string Name { get; }

    public IBrush Brush { get; }

    public bool IsSelected
    {
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected, value);
    }

    public RelayCommand SelectCommand { get; }
}
