namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One entry in a dropdown: the value it stands for, and the words shown for it. A ComboBox draws
/// an item by its <see cref="ToString"/>, so this is all a dropdown of enum values needs to stop
/// showing "AppleMusic" and "Urls".
/// </summary>
public sealed record Choice<T>(T Value, string Label)
{
    public override string ToString() => this.Label;
}
