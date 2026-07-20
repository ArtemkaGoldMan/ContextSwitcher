namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One editable quick link row in a Profile Setup page. Not to be confused with
/// <see cref="QuickLinkViewModel"/>, the read-only row shown on the Dashboard.
/// </summary>
public sealed class QuickLinkRowViewModel : ViewModelBase
{
    private string title;
    private string url;
    private string icon;

    public QuickLinkRowViewModel(string title, string url, string icon, Action<QuickLinkRowViewModel> remove)
    {
        this.title = title;
        this.url = url;
        this.icon = icon;
        this.RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Title
    {
        get => this.title;
        set => this.SetProperty(ref this.title, value);
    }

    public string Url
    {
        get => this.url;
        set => this.SetProperty(ref this.url, value);
    }

    public string Icon
    {
        get => this.icon;
        set => this.SetProperty(ref this.icon, value);
    }

    public RelayCommand RemoveCommand { get; }
}
