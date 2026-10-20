using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.App.Resources;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One row in a Profile Setup's browser-profiles list (<c>browser_management.profiles[]</c>,
/// docs/configuration.md). The profile is chosen from the ones the browser actually has, by the
/// name the browser shows for it, rather than typed as a folder name like "Profile 2". Chromium
/// profile URLs are edited as one URL per line rather than a nested add/remove list, since a
/// profile row already nests inside the browser-management section. The browser is the section's:
/// Profile Setup sets it on every row.
/// </summary>
public sealed class BrowserProfileRowViewModel : ViewModelBase
{
    private readonly Func<BrowserKind, IReadOnlyList<BrowserProfile>> profilesOf;

    private BrowserKind browser;
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

        // One whose profiles can be opened: Profile Setup gives no other.
        this.browser = browser;
        this.urlsText = string.Join(Environment.NewLine, urls);
        this.RefreshProfiles(profileDirectory.Trim());
        this.RemoveCommand = new RelayCommand(() => remove(this));
    }

    /// <summary>The browser whose profile this is - one whose profiles another app can open.</summary>
    public BrowserKind Browser
    {
        get => this.browser;
        set
        {
            if (KnownBrowsers.HasProfiles(value) && this.SetProperty(ref this.browser, value))
            {
                // Folder names are per browser; one Chrome has may not exist in Brave.
                this.RefreshProfiles(this.ProfileDirectory);
            }
        }
    }

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
    /// Whether the browser has profiles to choose from. One that has never been opened has none yet
    /// - it makes its first when it first starts - and none is made up for it.
    /// </summary>
    public bool HasProfiles => this.ProfileChoices.Count > 0;

    /// <summary>Why there is nothing to choose, when there isn't.</summary>
    public string NoProfilesText =>
        Localized.Format(Strings.Browser_NoProfiles, KnownBrowsers.Name(this.Browser));

    /// <summary>
    /// Rebuilds the profile list for the current browser and selects <paramref name="keep"/>: only
    /// the profiles the browser has. A saved folder the browser no longer has stays listed, marked,
    /// rather than vanishing.
    /// </summary>
    private void RefreshProfiles(string keep)
    {
        List<Choice<string>> choices = this.profilesOf(this.Browser)
            .Select(profile => new Choice<string>(
                profile.Directory,
                profile.Name == profile.Directory ? profile.Name : $"{profile.Name} ({profile.Directory})"))
            .ToList();

        Choice<string>? match = choices.FirstOrDefault(choice => choice.Value == keep);
        if (match is null && keep.Length > 0 && this.selectedProfile is null)
        {
            match = new Choice<string>(keep, Localized.Format(Strings.Browser_ProfileNotFound, keep));
            choices.Add(match);
        }

        this.ProfileChoices = choices;
        this.selectedProfile = match ?? choices.FirstOrDefault();
        this.OnPropertyChanged(nameof(this.SelectedProfile));
        this.OnPropertyChanged(nameof(this.ProfileDirectory));
        this.OnPropertyChanged(nameof(this.HasProfiles));
        this.OnPropertyChanged(nameof(this.NoProfilesText));
    }
}
