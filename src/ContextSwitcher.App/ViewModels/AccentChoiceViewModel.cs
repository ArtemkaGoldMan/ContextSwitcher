using Avalonia.Media;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One entry in Profile Setup's accent color dropdown: a preset, or "Custom", whose swatch follows
/// whatever the color picker it reveals is set to.
/// </summary>
public sealed class AccentChoiceViewModel : ViewModelBase
{
    private IBrush brush;

    private AccentChoiceViewModel(string? hex, string name, IBrush brush)
    {
        this.Hex = hex;
        this.Name = name;
        this.brush = brush;
    }

    /// <summary>The preset's color; null for <see cref="IsCustom"/>.</summary>
    public string? Hex { get; }

    public string Name { get; }

    public bool IsCustom => this.Hex is null;

    public IBrush Brush
    {
        get => this.brush;
        set => this.SetProperty(ref this.brush, value);
    }

    public static AccentChoiceViewModel Preset(string hex, string name) => new(hex, name, AccentColorParser.ToBrush(hex));

    public static AccentChoiceViewModel Custom(string hex) => new(null, "Custom", AccentColorParser.ToBrush(hex));

    public override string ToString() => this.Name;
}
