namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One tile in Profile Setup's icon picker.
/// </summary>
public sealed class IconChoiceViewModel : ViewModelBase
{
    private bool isSelected;

    public IconChoiceViewModel(ProfileIcon icon, Action<string> select)
    {
        this.Name = icon.Name;
        this.Label = icon.Label;
        this.SelectCommand = new RelayCommand(() => select(icon.Name));
    }

    /// <summary>The stored icon name; the tile's glyph is drawn from it by ProfileIconConverter.</summary>
    public string Name { get; }

    public string Label { get; }

    public bool IsSelected
    {
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected, value);
    }

    public RelayCommand SelectCommand { get; }
}
