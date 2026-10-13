using Avalonia.Media.Imaging;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One row in a Profile Setup's app list. Presents the on-disk <c>launchApps</c>/<c>closeApps</c>/
/// <c>hideApps</c> flat lists (agent.md section 6.1) as a single row per app: Launch on entering, and
/// on leaving either Close or Hide - ticking one unticks the other, as an app is quit or hidden, not
/// both.
/// </summary>
public sealed class AppRowViewModel : ViewModelBase
{
    private string name;
    private bool launchOnEnter;
    private bool closeOnLeave;
    private bool hideOnLeave;
    private Bitmap? icon;

    /// <param name="isEditable">
    /// True only for a row added with "Add manually…". An app chosen from the picker or loaded from
    /// config is shown by name, not as a text box - its name is exactly what <c>open -a</c> needs,
    /// and the only edit to make to it is a typo.
    /// </param>
    public AppRowViewModel(string name, bool launchOnEnter, bool closeOnLeave, Action<AppRowViewModel> remove, bool isEditable = false, bool hideOnLeave = false)
    {
        this.name = name;
        this.IsEditable = isEditable;
        this.launchOnEnter = launchOnEnter;
        this.closeOnLeave = closeOnLeave;
        this.hideOnLeave = hideOnLeave && !closeOnLeave;
        this.RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Name
    {
        get => this.name;
        set => this.SetProperty(ref this.name, value);
    }

    public bool IsEditable { get; }

    public bool LaunchOnEnter
    {
        get => this.launchOnEnter;
        set => this.SetProperty(ref this.launchOnEnter, value);
    }

    public bool CloseOnLeave
    {
        get => this.closeOnLeave;
        set
        {
            if (this.SetProperty(ref this.closeOnLeave, value) && value)
            {
                this.HideOnLeave = false;
            }
        }
    }

    /// <summary>Hidden rather than quit on leaving: it keeps running, and comes back with its windows.</summary>
    public bool HideOnLeave
    {
        get => this.hideOnLeave;
        set
        {
            if (this.SetProperty(ref this.hideOnLeave, value) && value)
            {
                this.CloseOnLeave = false;
            }
        }
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
