using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One row in a Profile Setup's browser-profiles list (<c>browser_management.profiles[]</c>,
/// agent.md section 6.1). Chromium profile URLs are edited as one URL per line rather than a
/// nested add/remove list, since a profile row already nests inside the browser-management section.
/// </summary>
public sealed class BrowserProfileRowViewModel : ViewModelBase
{
    /// <summary>
    /// The only browsers <see cref="ConfigurationValidator"/> accepts for profile-directory mode.
    /// </summary>
    public static IReadOnlyList<BrowserKind> SupportedBrowsers { get; } = [BrowserKind.Chrome, BrowserKind.Brave];

    private BrowserKind browser;
    private string profileDirectory;
    private string urlsText;

    public BrowserProfileRowViewModel(BrowserKind browser, string profileDirectory, IReadOnlyList<string> urls, Action<BrowserProfileRowViewModel> remove)
    {
        this.browser = browser;
        this.profileDirectory = profileDirectory;
        this.urlsText = string.Join(Environment.NewLine, urls);
        this.RemoveCommand = new RelayCommand(() => remove(this));
    }

    public BrowserKind Browser
    {
        get => this.browser;
        set => this.SetProperty(ref this.browser, value);
    }

    public string ProfileDirectory
    {
        get => this.profileDirectory;
        set => this.SetProperty(ref this.profileDirectory, value);
    }

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
}
