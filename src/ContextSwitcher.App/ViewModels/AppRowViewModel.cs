using Avalonia.Media.Imaging;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One row in a Profile Setup's app list. Presents the on-disk <c>launchApps</c>/<c>closeApps</c>
/// flat lists (agent.md section 6.1) as a single row per app with two independent toggles - the
/// friendlier editing surface described in section 11.1.2.
/// </summary>
public sealed class AppRowViewModel : ViewModelBase
{
    private string name;
    private bool launchOnEnter;
    private bool closeOnLeave;
    private Bitmap? icon;

    public AppRowViewModel(string name, bool launchOnEnter, bool closeOnLeave, Action<AppRowViewModel> remove)
    {
        this.name = name;
        this.launchOnEnter = launchOnEnter;
        this.closeOnLeave = closeOnLeave;
        this.RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Name
    {
        get => this.name;
        set => this.SetProperty(ref this.name, value);
    }

    public bool LaunchOnEnter
    {
        get => this.launchOnEnter;
        set => this.SetProperty(ref this.launchOnEnter, value);
    }

    public bool CloseOnLeave
    {
        get => this.closeOnLeave;
        set => this.SetProperty(ref this.closeOnLeave, value);
    }

    /// <summary>
    /// The app's real icon when it could be matched to an installed bundle. Null for apps that
    /// aren't installed (or were typed manually), in which case the view shows a neutral
    /// placeholder rather than leaving a gap.
    /// </summary>
    public Bitmap? Icon
    {
        get => this.icon;
        set
        {
            if (this.SetProperty(ref this.icon, value))
            {
                this.OnPropertyChanged(nameof(this.HasIcon));
            }
        }
    }

    public bool HasIcon => this.Icon is not null;

    public RelayCommand RemoveCommand { get; }
}
