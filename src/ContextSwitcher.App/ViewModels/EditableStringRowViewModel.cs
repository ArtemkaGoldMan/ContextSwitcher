namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One editable row in an add/remove list of plain strings (browser URLs, tab groups, Docker
/// container names) - section 11.2's "list rows with add/remove buttons for app/browser/docker
/// arrays".
/// </summary>
public sealed class EditableStringRowViewModel : ViewModelBase
{
    private string value;

    public EditableStringRowViewModel(string value, Action<EditableStringRowViewModel> remove)
    {
        this.value = value;
        this.RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Value
    {
        get => this.value;
        set => this.SetProperty(ref this.value, value);
    }

    public RelayCommand RemoveCommand { get; }
}
