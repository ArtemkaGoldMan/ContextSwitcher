using Avalonia.Media.Imaging;
using ContextSwitcher.App.Resources;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.App.ViewModels;

/// <summary>How the window asking about everything else open ended.</summary>
/// <param name="Skipped">Closed or skipped: switch as if it hadn't asked.</param>
/// <param name="DontAskAgain">Stop asking - turns <see cref="AppConfiguration.AskOnSwitch"/> off.</param>
/// <param name="Answers">What to do with each app and tab.</param>
public sealed record AskOnSwitchResult(bool Skipped, bool DontAskAgain, UnclaimedAnswers Answers);

/// <summary>
/// Backs the window a switch opens, when <see cref="AppConfiguration.AskOnSwitch"/> is on, about the
/// apps and tabs open that neither profile in it has (<see cref="UnclaimedItems"/>). Each can be kept,
/// closed, or added to the profile being left or the one being entered; everything starts as kept, so
/// nothing closes that wasn't chosen. More than <see cref="MostTabsListed"/> tabs are one line - keep
/// or close them all - rather than a list to scroll through.
/// </summary>
public sealed class AskOnSwitchViewModel : ViewModelBase
{
    /// <summary>The most tabs listed one by one.</summary>
    public const int MostTabsListed = 13;

    private readonly Unclaimed unclaimed;
    private readonly TaskCompletionSource<AskOnSwitchResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool dontAskAgain;

    public AskOnSwitchViewModel(Unclaimed unclaimed, ContextDefinition? leaving, ContextDefinition? entering)
    {
        ArgumentNullException.ThrowIfNull(unclaimed);
        this.unclaimed = unclaimed;

        (this.Title, this.Subtitle) = (leaving, entering) switch
        {
            ({ } from, { } to) => (Localized.Format(Strings.Ask_Title_Switch, from.DisplayName, to.DisplayName), Strings.Ask_Subtitle_Two),
            ({ } from, null) => (Localized.Format(Strings.Ask_Title_Off, from.DisplayName), Localized.Format(Strings.Ask_Subtitle_One, from.DisplayName)),
            (null, { } to) => (Localized.Format(Strings.Ask_Title_On, to.DisplayName), Localized.Format(Strings.Ask_Subtitle_One, to.DisplayName)),
            _ => (string.Empty, string.Empty)
        };

        List<(UnclaimedChoice, string)> appOptions = [.. KeepOrClose(), .. AddTo(leaving, entering, _ => true)];
        List<(UnclaimedChoice, string)> tabOptions = [.. KeepOrClose(), .. AddTo(leaving, entering, UnclaimedAnswers.TakesPages)];

        this.Apps = unclaimed.Apps.Select(app => new UnclaimedRowViewModel(app.Name, null, isApp: true, appOptions)).ToList();
        if (unclaimed.Tabs.Count > MostTabsListed)
        {
            this.Tabs = [];
            this.AllTabs = new UnclaimedRowViewModel(Localized.Format(Strings.Ask_ManyTabs, unclaimed.Tabs.Count), null, isApp: false, KeepOrClose());
        }
        else
        {
            this.Tabs = unclaimed.Tabs.Select(tab => new UnclaimedRowViewModel(tab.Title, HostOf(tab.Url), isApp: false, tabOptions, tab.Url)).ToList();
        }

        this.KeepAllCommand = new RelayCommand(() => this.ChooseForAll(UnclaimedChoice.Keep));
        this.CloseAllCommand = new RelayCommand(() => this.ChooseForAll(UnclaimedChoice.Close));
        this.SkipCommand = new RelayCommand(this.Skip);
        this.ContinueCommand = new RelayCommand(() => this.completion.TrySetResult(new AskOnSwitchResult(false, this.DontAskAgain, this.Answers())));
    }

    public string Title { get; }

    public string Subtitle { get; }

    public IReadOnlyList<UnclaimedRowViewModel> Apps { get; }

    public bool HasApps => this.Apps.Count > 0;

    public string AppsHeader => Localized.Format(Strings.Ask_Apps, this.Apps.Count);

    /// <summary>The tabs one by one - or none, when there are too many and <see cref="AllTabs"/> stands for them.</summary>
    public IReadOnlyList<UnclaimedRowViewModel> Tabs { get; }

    /// <summary>All the tabs as one line, keep or close, when there are more than <see cref="MostTabsListed"/>.</summary>
    public UnclaimedRowViewModel? AllTabs { get; }

    public bool HasTabs => this.unclaimed.Tabs.Count > 0;

    public bool HasManyTabs => this.AllTabs is not null;

    public string TabsHeader => Localized.Format(Strings.Ask_Tabs, this.unclaimed.Tabs.Count);

    /// <summary>Stop asking: turns the setting off, which Settings can turn back on.</summary>
    public bool DontAskAgain
    {
        get => this.dontAskAgain;
        set => this.SetProperty(ref this.dontAskAgain, value);
    }

    public RelayCommand KeepAllCommand { get; }

    public RelayCommand CloseAllCommand { get; }

    public RelayCommand SkipCommand { get; }

    public RelayCommand ContinueCommand { get; }

    /// <summary>The answer, once given - or the window closed, which is a skip.</summary>
    public Task<AskOnSwitchResult> Completion => this.completion.Task;

    /// <summary>Goes on with the switch touching nothing else - the window was skipped or closed.</summary>
    public void Skip() =>
        this.completion.TrySetResult(new AskOnSwitchResult(true, this.DontAskAgain, new UnclaimedAnswers([], [])));

    /// <summary>Shows the apps' own icons once they're found; they're looked up after the window opens, so it never waits on them.</summary>
    public async Task LoadIconsAsync(IInstalledAppsService installedApps, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(installedApps);
        if (this.Apps.Count == 0)
        {
            return;
        }

        IReadOnlyList<Core.Applications.InstalledApp> installed = await installedApps.GetInstalledAppsAsync(cancellationToken).ConfigureAwait(true);
        foreach (UnclaimedRowViewModel row in this.Apps)
        {
            string? iconPath = installed.FirstOrDefault(app => string.Equals(app.Name, row.Title, StringComparison.OrdinalIgnoreCase))?.IconPath;
            if (iconPath is not null)
            {
                row.Icon = TryLoad(iconPath);
            }
        }
    }

    /// <summary>
    /// Shows each tab's site icon once found, from the site itself (<see cref="ISiteIcons"/>) - each site
    /// asked once however many of its tabs are open, all at once, after the window has opened. A site
    /// with no icon, or too slow to give one, keeps the globe.
    /// </summary>
    public async Task LoadSiteIconsAsync(ISiteIcons siteIcons, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(siteIcons);

        IEnumerable<Task> sites = this.Tabs
            .Where(row => row.Url is not null)
            .GroupBy(row => HostOf(row.Url!), StringComparer.OrdinalIgnoreCase)
            .Select(async site =>
            {
                string? path = await siteIcons.GetIconPathAsync(site.First().Url!, cancellationToken).ConfigureAwait(true);
                Bitmap? icon = path is null ? null : TryLoad(path);
                foreach (UnclaimedRowViewModel row in site)
                {
                    row.Icon = icon;
                }
            });
        await Task.WhenAll(sites).ConfigureAwait(true);
    }

    private static Bitmap? TryLoad(string path)
    {
        try
        {
            return File.Exists(path) ? new Bitmap(path) : null;
        }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private UnclaimedAnswers Answers()
    {
        List<(Core.Applications.RunningApp, UnclaimedChoice)> apps = this.unclaimed.Apps
            .Select((app, i) => (app, this.Apps[i].Choice))
            .ToList();
        List<(Core.Catalog.OpenBrowserTab, UnclaimedChoice)> tabs = this.AllTabs is { } all
            ? this.unclaimed.Tabs.Select(tab => (tab, all.Choice)).ToList()
            : this.unclaimed.Tabs.Select((tab, i) => (tab, this.Tabs[i].Choice)).ToList();
        return new UnclaimedAnswers(apps, tabs);
    }

    private void ChooseForAll(UnclaimedChoice choice)
    {
        foreach (UnclaimedRowViewModel row in this.Apps.Concat(this.Tabs))
        {
            row.Choice = choice;
        }

        if (this.AllTabs is { } all)
        {
            all.Choice = choice;
        }
    }

    private static IEnumerable<(UnclaimedChoice, string)> KeepOrClose() =>
        [(UnclaimedChoice.Keep, Strings.Ask_Keep), (UnclaimedChoice.Close, Strings.Ask_Close)];

    private static IEnumerable<(UnclaimedChoice, string)> AddTo(ContextDefinition? leaving, ContextDefinition? entering, Func<ContextDefinition?, bool> takes)
    {
        if (leaving is not null && takes(leaving))
        {
            yield return (UnclaimedChoice.AddToLeaving, Localized.Format(Strings.Ask_AddTo, leaving.DisplayName));
        }

        if (entering is not null && takes(entering))
        {
            yield return (UnclaimedChoice.AddToEntering, Localized.Format(Strings.Ask_AddTo, entering.DisplayName));
        }
    }

    private static string HostOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host : url;
}

/// <summary>An app or a tab in the window, with what is to be done with it.</summary>
public sealed class UnclaimedRowViewModel : ViewModelBase
{
    private UnclaimedChoice choice = UnclaimedChoice.Keep;
    private Bitmap? icon;

    public UnclaimedRowViewModel(string title, string? subtitle, bool isApp, IEnumerable<(UnclaimedChoice Value, string Label)> options, string? url = null)
    {
        this.Title = title;
        this.Subtitle = subtitle;
        this.IsApp = isApp;
        this.Url = url;
        this.Options = options.Select(option => new UnclaimedOptionViewModel(this, option.Value, option.Label)).ToList();
    }

    public string Title { get; }

    public string? Subtitle { get; }

    public bool HasSubtitle => !string.IsNullOrEmpty(this.Subtitle);

    public bool IsApp { get; }

    /// <summary>A tab's address, whose site's icon it shows.</summary>
    public string? Url { get; }

    public bool HasAppIcon => this.IsApp && this.HasIcon;

    public bool HasSiteIcon => !this.IsApp && this.HasIcon;

    public Bitmap? Icon
    {
        get => this.icon;
        set
        {
            if (this.SetProperty(ref this.icon, value))
            {
                this.OnPropertyChanged(nameof(this.HasIcon));
                this.OnPropertyChanged(nameof(this.HasAppIcon));
                this.OnPropertyChanged(nameof(this.HasSiteIcon));
            }
        }
    }

    public bool HasIcon => this.Icon is not null;

    public IReadOnlyList<UnclaimedOptionViewModel> Options { get; }

    public UnclaimedChoice Choice
    {
        get => this.choice;
        set
        {
            if (this.Options.Any(option => option.Value == value) && this.SetProperty(ref this.choice, value))
            {
                foreach (UnclaimedOptionViewModel option in this.Options)
                {
                    option.Refresh();
                }
            }
        }
    }
}

/// <summary>One of a row's choices, shown as a segment that is lit when chosen.</summary>
public sealed class UnclaimedOptionViewModel : ViewModelBase
{
    private readonly UnclaimedRowViewModel row;

    public UnclaimedOptionViewModel(UnclaimedRowViewModel row, UnclaimedChoice value, string label)
    {
        this.row = row;
        this.Value = value;
        this.Label = label;
        this.SelectCommand = new RelayCommand(() => row.Choice = value);
    }

    public UnclaimedChoice Value { get; }

    public string Label { get; }

    public bool IsSelected => this.row.Choice == this.Value;

    public RelayCommand SelectCommand { get; }

    internal void Refresh() => this.OnPropertyChanged(nameof(this.IsSelected));
}
