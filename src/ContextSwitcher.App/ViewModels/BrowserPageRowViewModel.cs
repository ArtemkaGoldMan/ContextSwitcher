namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One page in a Profile Setup's list, shown the way an app is: one row with two independent
/// toggles, over the on-disk <c>urls</c> (opened on entering) and <c>close_urls</c> (closed on
/// leaving) lists.
/// </summary>
public sealed class BrowserPageRowViewModel : ViewModelBase
{
    private string url;
    private bool openOnEnter;
    private bool closeOnLeave;
    private bool canClose;

    public BrowserPageRowViewModel(string url, bool openOnEnter, bool closeOnLeave, bool canClose, Action<BrowserPageRowViewModel> remove)
    {
        this.url = url;
        this.openOnEnter = openOnEnter;
        this.closeOnLeave = closeOnLeave;
        this.canClose = canClose;
        this.RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Url
    {
        get => this.url;
        set => this.SetProperty(ref this.url, value);
    }

    public bool OpenOnEnter
    {
        get => this.openOnEnter;
        set => this.SetProperty(ref this.openOnEnter, value);
    }

    public bool CloseOnLeave
    {
        get => this.closeOnLeave;
        set => this.SetProperty(ref this.closeOnLeave, value);
    }

    /// <summary>Whether the profile's browser can be asked for its tabs - not so for the default one.</summary>
    public bool CanClose
    {
        get => this.canClose;
        set => this.SetProperty(ref this.canClose, value);
    }

    public RelayCommand RemoveCommand { get; }
}
