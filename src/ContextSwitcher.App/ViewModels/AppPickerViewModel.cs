using Avalonia.Media.Imaging;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Shared "choose an installed app" list, used by both Profile Setup and the first-run wizard.
/// Owns the one-time scan, the decoded icon cache, the search filter, and exclusion of apps the
/// caller has already added.
/// </summary>
public sealed class AppPickerViewModel : ViewModelBase
{
    private readonly IInstalledAppsService installedAppsService;
    private readonly Func<IEnumerable<string>> getExcludedNames;
    private readonly Action<string> onPicked;
    private readonly Dictionary<string, Bitmap?> iconsByAppName = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<InstalledAppViewModel> allApps = [];
    private IReadOnlyList<InstalledAppViewModel> filteredApps = [];
    private string searchText = string.Empty;
    private bool isLoading;

    /// <param name="getExcludedNames">
    /// App names already chosen by the caller, re-evaluated on every filter pass so the list stays
    /// in sync as rows are added and removed.
    /// </param>
    public AppPickerViewModel(
        IInstalledAppsService installedAppsService,
        Func<IEnumerable<string>> getExcludedNames,
        Action<string> onPicked)
    {
        this.installedAppsService = installedAppsService;
        this.getExcludedNames = getExcludedNames;
        this.onPicked = onPicked;
    }

    public IReadOnlyList<InstalledAppViewModel> FilteredApps
    {
        get => this.filteredApps;
        private set
        {
            if (this.SetProperty(ref this.filteredApps, value))
            {
                this.OnPropertyChanged(nameof(this.HasNoMatches));
            }
        }
    }

    public string SearchText
    {
        get => this.searchText;
        set
        {
            if (this.SetProperty(ref this.searchText, value))
            {
                this.Refresh();
            }
        }
    }

    public bool IsLoading
    {
        get => this.isLoading;
        private set
        {
            if (this.SetProperty(ref this.isLoading, value))
            {
                this.OnPropertyChanged(nameof(this.HasNoMatches));
            }
        }
    }

    public bool HasNoMatches => !this.IsLoading && this.FilteredApps.Count == 0;

    /// <summary>
    /// All discovered apps in scan order, for callers that want to seed suggestions rather than
    /// present the picker (the onboarding templates).
    /// </summary>
    public IReadOnlyList<InstalledAppViewModel> AllApps => this.allApps;

    /// <summary>
    /// Scans once. Never throws: this is invoked fire-and-forget, so anything escaping would become
    /// an unobserved task exception and vanish (which is how a missing `sips` allowlist entry hid
    /// itself once already). The picker degrades to manual entry instead.
    /// </summary>
    public async Task LoadAsync()
    {
        this.IsLoading = true;
        try
        {
            IReadOnlyList<InstalledApp> installed = await this.installedAppsService
                .GetInstalledAppsAsync(CancellationToken.None)
                .ConfigureAwait(true);

            List<InstalledAppViewModel> viewModels = [];
            foreach (InstalledApp app in installed)
            {
                Bitmap? icon = LoadIcon(app.IconPath);
                this.iconsByAppName[app.Name] = icon;
                viewModels.Add(new InstalledAppViewModel(app.Name, icon, this.onPicked));
            }

            this.allApps = viewModels;
        }
        catch (Exception)
        {
            this.allApps = [];
        }
        finally
        {
            this.IsLoading = false;
            this.Refresh();
        }
    }

    /// <summary>
    /// Re-applies the exclusion and search filters. Call after the caller's chosen set changes.
    /// </summary>
    public void Refresh()
    {
        HashSet<string> excluded = this.getExcludedNames()
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IEnumerable<InstalledAppViewModel> matches = this.allApps.Where(app => !excluded.Contains(app.Name));

        if (!string.IsNullOrWhiteSpace(this.SearchText))
        {
            matches = matches.Where(app => app.Name.Contains(this.SearchText.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        this.FilteredApps = matches.ToList();
    }

    /// <summary>
    /// The cached icon for an app name, or <see langword="null"/> when it isn't installed.
    /// </summary>
    public Bitmap? ResolveIcon(string appName)
    {
        return this.iconsByAppName.TryGetValue(appName, out Bitmap? icon) ? icon : null;
    }

    private static Bitmap? LoadIcon(string? iconPath)
    {
        if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath))
        {
            return null;
        }

        try
        {
            return new Bitmap(iconPath);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            // A truncated or unreadable cached PNG must not break the picker.
            return null;
        }
    }
}
