using ContextSwitcher.App.Resources;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>Profile Setup's look: the profile's colour and its icon.</summary>
public sealed class ProfileLookViewModel : ViewModelBase
{
    /// <summary>
    /// The presets, chosen to stay legible under the white glyph drawn on top of them, which rules
    /// out a yellow; the first and the green are the two colors onboarding gives its profiles.
    /// </summary>
    private static readonly (string Hex, string Name)[] AccentPalette =
    [
        ("#2F6FED", Strings.Accent_Blue),
        ("#7B61FF", Strings.Accent_Violet),
        ("#D6409F", Strings.Accent_Pink),
        ("#E5484D", Strings.Accent_Red),
        ("#F2994A", Strings.Accent_Orange),
        ("#20A67A", Strings.Accent_Green),
        ("#0FA3B1", Strings.Accent_Teal),
        ("#6E7781", Strings.Accent_Graphite)
    ];

    private readonly AccentChoiceViewModel customAccent;
    private string accentColor;
    private bool customAccentChosen;
    private string icon;

    public ProfileLookViewModel(string accentColor, string icon)
    {
        this.accentColor = accentColor;
        this.customAccent = AccentChoiceViewModel.Custom(accentColor);
        this.AccentChoices = [.. AccentPalette.Select(entry => AccentChoiceViewModel.Preset(entry.Hex, entry.Name)), this.customAccent];
        this.icon = icon;
        this.IconChoices = ProfileIcons.All.Select(choice => new IconChoiceViewModel(choice)).ToList();
    }

    public string AccentColor
    {
        get => this.accentColor;
        set
        {
            if (this.SetProperty(ref this.accentColor, value))
            {
                this.customAccent.Brush = this.AccentBrush;
                this.OnPropertyChanged(nameof(this.AccentBrush));
                this.OnPropertyChanged(nameof(this.AccentColorValue));
                this.OnPropertyChanged(nameof(this.IsCustomAccent));
                this.OnPropertyChanged(nameof(this.SelectedAccent));
            }
        }
    }

    public Avalonia.Media.IBrush AccentBrush => AccentColorParser.ToBrush(this.AccentColor);

    /// <summary>The accent dropdown: the presets, then "Custom".</summary>
    public IReadOnlyList<AccentChoiceViewModel> AccentChoices { get; }

    public AccentChoiceViewModel SelectedAccent
    {
        get => this.IsCustomAccent ? this.customAccent : this.MatchingPreset() ?? this.customAccent;
        set
        {
            // A ComboBox writes null while its items are being swapped; that is not a choice.
            if (value is null)
            {
                return;
            }

            this.customAccentChosen = value.IsCustom;
            if (value.Hex is not null)
            {
                this.AccentColor = value.Hex;
            }

            this.OnPropertyChanged(nameof(this.IsCustomAccent));
            this.OnPropertyChanged(nameof(this.SelectedAccent));
        }
    }

    /// <summary>
    /// The color is custom, so the color picker shows under the dropdown: either "Custom" was chosen,
    /// or the color is none of the presets (a saved custom color, or one typed into the config). It
    /// stays custom while the picker is dragged, even across a preset's exact color, so the picker
    /// does not vanish from under the pointer.
    /// </summary>
    public bool IsCustomAccent => this.customAccentChosen || this.MatchingPreset() is null;

    /// <summary>
    /// The accent as a <see cref="Avalonia.Media.Color"/> for the custom color picker. Config keeps
    /// the plain #RRGGBB string it always has; the picker's alpha is ignored.
    /// </summary>
    public Avalonia.Media.Color AccentColorValue
    {
        get => Avalonia.Media.Color.TryParse(this.AccentColor, out Avalonia.Media.Color color) ? color : Avalonia.Media.Colors.Gray;
        set => this.AccentColor = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
    }

    /// <summary>
    /// The stored icon name. An unknown name - a hand-edited config, or one saved by a build with more
    /// icons - is kept as-is until the user picks another, and is drawn as the fallback meanwhile.
    /// </summary>
    public string Icon
    {
        get => this.icon;
        set
        {
            if (this.SetProperty(ref this.icon, value))
            {
                this.OnPropertyChanged(nameof(this.SelectedIcon));
            }
        }
    }

    /// <summary>Every icon a profile can use, each drawn beside its name in the dropdown.</summary>
    public IReadOnlyList<IconChoiceViewModel> IconChoices { get; }

    /// <summary>
    /// The dropdown's entry for <see cref="Icon"/>, or the fallback's for a name this build does not
    /// know - which only shows it; the stored name changes only when another icon is picked.
    /// </summary>
    public IconChoiceViewModel SelectedIcon
    {
        get => this.IconChoices.First(choice => choice.Name == ProfileIcons.Find(this.Icon).Name);
        set
        {
            if (value is not null)
            {
                this.Icon = value.Name;
            }
        }
    }

    private AccentChoiceViewModel? MatchingPreset() =>
        this.AccentChoices.FirstOrDefault(choice => string.Equals(choice.Hex, this.AccentColor?.Trim(), StringComparison.OrdinalIgnoreCase));
}
