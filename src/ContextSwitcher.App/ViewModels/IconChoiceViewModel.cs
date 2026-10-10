namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One entry in Profile Setup's icon dropdown, drawn as its glyph beside its label.
/// </summary>
public sealed class IconChoiceViewModel(ProfileIcon icon)
{
    /// <summary>The stored icon name; the glyph is drawn from it by ProfileIconConverter.</summary>
    public string Name { get; } = icon.Name;

    public string Label { get; } = icon.Label;

    public override string ToString() => this.Label;
}
