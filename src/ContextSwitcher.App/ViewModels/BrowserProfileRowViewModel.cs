using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One row in a Profile Setup's browser-profiles list (<c>browser_management.profiles[]</c>,
/// agent.md section 6.1). The profile is chosen from the ones the browser actually has, by the
/// name the browser shows for it, rather than typed as a folder name like "Profile 2". Chromium
/// profile URLs are edited as one URL per line rather than a nested add/remove list, since a
/// profile row already nests inside the browser-management section.
/// </summary>
public sealed class BrowserProfileRowViewModel : ViewModelBase
{
    /// <summary>
    /// The only browsers <see cref="ConfigurationValidator"/> accepts for profile-directory mode.
    /// </summary>
    public static IReadOnlyList<Choice<BrowserKind>> BrowserChoices { get; } =
    [
        new(BrowserKind.Chrome, "Google Chrome"),
        new(BrowserKind.Brave, "Brave")
    ];

    private readonly Func<BrowserKind, IReadOnlyList<BrowserProfile>> profilesOf;

    private Choice<BrowserKind> selectedBrowser;
    private IReadOnlyList<Choice<string>> profileChoices = [];
    private Choice<string>? selectedProfile;
    private string urlsText;

    /// <param name="profileDirectory">The saved folder name, or empty for a new row.</param>
    /// <param name="profilesOf">Reads a browser's profiles.</param>
    public BrowserProfileRowViewModel(
        BrowserKind browser,
        string profileDirectory,
        IReadOnlyList<string> urls,
        Func<BrowserKind, IReadOnlyList<BrowserProfile>> profilesOf,
        Action<BrowserProfileRowViewModel> remove)
    {
        this.profilesOf = profilesOf;
        this.selectedBrowser = BrowserChoices.FirstOrDefault(choice => choice.Value == browser) ?? BrowserChoices[0];
        this.urlsText = string.Join(Environment.NewLine, urls);
        this.RefreshProfiles(profileDirectory.Trim());
        this.RemoveCommand = new RelayCommand(() => remove(this));
    }

    public Choice<BrowserKind> SelectedBrowser
    {
        get => this.selectedBrowser;
        set
        {
            if (value is not null && this.SetProperty(ref this.selectedBrowser, value))
            {
                this.OnPropertyChanged(nameof(this.Browser));

                // Folder names are per browser; one Chrome has may not exist in Brave.
                this.RefreshProfiles(this.ProfileDirectory);
            }
        }
    }

    public BrowserKind Browser => this.SelectedBrowser.Value;

    /// <summary>The browser's profiles, labelled with the name it shows for each.</summary>
    public IReadOnlyList<Choice<string>> ProfileChoices
    {
        get => this.profileChoices;
        private set => this.SetProperty(ref this.profileChoices, value);
    }

    public Choice<string>? SelectedProfile
    {
        get => this.selectedProfile;
        set
        {
            if (value is not null && this.SetProperty(ref this.selectedProfile, value))
            {
                this.OnPropertyChanged(nameof(this.ProfileDirectory));
            }
        }
    }

    /// <summary>The folder name passed to <c>--profile-directory</c>.</summary>
    public string ProfileDirectory => this.SelectedProfile?.Value ?? string.Empty;

    /// <summary>
    /// One URL per line, parsed into <see cref="BrowserProfileConfig.Urls"/> on save.
    /// </summary>
    public string UrlsText
    {
        get => this.urlsText;
        set => this.SetProperty(ref this.urlsText, value);
    }

    public RelayCommand RemoveCommand { get; }

    public IReadOnlyList<string> ParseUrls()
    {
        return this.UrlsText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>
    /// Rebuilds the profile list for the current browser and selects <paramref name="keep"/>. A
    /// saved folder the browser no longer has stays listed, marked, rather than vanishing - and a
    /// browser with no readable profiles still offers Default, which every Chromium install has.
    /// </summary>
    private void RefreshProfiles(string keep)
    {
        List<Choice<string>> choices = this.profilesOf(this.Browser)
            .Select(profile => new Choice<string>(
                profile.Directory,
                profile.Name == profile.Directory ? profile.Name : $"{profile.Name} ({profile.Directory})"))
            .ToList();

        if (choices.Count == 0)
        {
            choices.Add(new Choice<string>("Default", "Default"));
        }

        Choice<string>? match = choices.FirstOrDefault(choice => choice.Value == keep);
        if (match is null && keep.Length > 0 && this.selectedProfile is null)
        {
            match = new Choice<string>(keep, $"{keep} (not found)");
            choices.Add(match);
        }

        this.ProfileChoices = choices;
        this.selectedProfile = match ?? choices[0];
        this.OnPropertyChanged(nameof(this.SelectedProfile));
        this.OnPropertyChanged(nameof(this.ProfileDirectory));
    }
}
