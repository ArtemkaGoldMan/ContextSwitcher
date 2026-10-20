namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One editable row in an add/remove list of plain strings (browser URLs, Docker container
/// names) - the "rows with add and remove for lists" of "Look and feel" in docs/architecture.md.
/// </summary>
public sealed class EditableStringRowViewModel : ViewModelBase
{
    private string value;

    /// <param name="isEditable">
    /// False for a value chosen from a list - a Docker container picked from the ones Docker has -
    /// which is shown as plain text rather than inviting an edit.
    /// </param>
    /// <param name="glyphKey">The icon resource in the row's leading slot: a globe, a box, layers.</param>
    /// <param name="placeholder">What the empty text box asks for.</param>
    public EditableStringRowViewModel(
        string value,
        Action<EditableStringRowViewModel> remove,
        bool isEditable = true,
        string glyphKey = "IconGlobe",
        string placeholder = "")
    {
        this.value = value;
        this.IsEditable = isEditable;
        this.GlyphKey = glyphKey;
        this.Placeholder = placeholder;
        this.RemoveCommand = new RelayCommand(() => remove(this));
    }

    public bool IsEditable { get; }

    public string GlyphKey { get; }

    public string Placeholder { get; }

    public string Value
    {
        get => this.value;
        set => this.SetProperty(ref this.value, value);
    }

    public RelayCommand RemoveCommand { get; }
}
