using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using ContextSwitcher.App.Resources;
using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Settings' list of apps ContextSwitcher never touches (<see cref="AppConfiguration.IgnoredApps"/>):
/// never opened, closed, hidden or asked about. Adding one takes it out of every profile, and says
/// which; removing one gives it back to no profile.
///
/// Light on memory: the picker reads every installed app and its icon only when it is opened, and the
/// list itself loads the icons of just the apps on it.
/// </summary>
public sealed class IgnoredAppsViewModel : ViewModelBase, IDisposable
{
    private readonly IAppState appState;
    private readonly ConfigurationStore configurationStore;
    private readonly IInstalledAppsService? installedApps;
    private readonly Dictionary<string, Bitmap?> icons = new(StringComparer.OrdinalIgnoreCase);
    private bool pickerLoaded;
    private string? message;

    public IgnoredAppsViewModel(IAppState appState, ConfigurationStore configurationStore, IInstalledAppsService? installedApps)
    {
        this.appState = appState;
        this.configurationStore = configurationStore;
        this.installedApps = installedApps;
        this.Apps = new ObservableCollection<IgnoredAppRowViewModel>();
        if (installedApps is not null)
        {
            this.Picker = new AppPickerViewModel(installedApps, () => this.Apps.Select(row => row.Name), name => _ = this.AddAsync(name));
        }

        this.OpenPickerCommand = new RelayCommand(() => _ = this.LoadPickerAsync());
        this.Reload();
        this.appState.ConfigurationChanged += this.OnConfigurationChanged;
        _ = this.LoadListedIconsAsync();
    }

    /// <summary>Opening the picker is when it reads the installed apps - not before.</summary>
    public RelayCommand OpenPickerCommand { get; }

    public ObservableCollection<IgnoredAppRowViewModel> Apps { get; }

    /// <summary>Picks an installed app to add; none where installed apps can't be read.</summary>
    public AppPickerViewModel? Picker { get; }

    public bool CanAdd => this.Picker is not null;

    /// <summary>What adding one did - which profiles it was taken out of.</summary>
    public string? Message
    {
        get => this.message;
        private set
        {
            if (this.SetProperty(ref this.message, value))
            {
                this.OnPropertyChanged(nameof(this.HasMessage));
            }
        }
    }

    public bool HasMessage => !string.IsNullOrEmpty(this.Message);

    public void Dispose() => this.appState.ConfigurationChanged -= this.OnConfigurationChanged;

    /// <summary>Never touches <paramref name="name"/> from now on, and takes it out of every profile.</summary>
    public async Task AddAsync(string name)
    {
        (AppConfiguration updated, IReadOnlyList<string> takenOutOf) = IgnoredApps.Ignore(this.appState.Configuration, name);
        ConfigurationSaveResult result = await this.configurationStore.SaveAsync(updated, CancellationToken.None).ConfigureAwait(true);
        this.Message = result.Succeeded && takenOutOf.Count > 0
            ? Localized.Format(Strings.Settings_IgnoredApp_TakenOut, name, string.Join(", ", takenOutOf))
            : null;
        this.Reload();
    }

    private async Task RemoveAsync(IgnoredAppRowViewModel row)
    {
        await this.configurationStore.SaveAsync(IgnoredApps.StopIgnoring(this.appState.Configuration, row.Name), CancellationToken.None).ConfigureAwait(true);
        this.Message = null;
        this.Reload();
    }

    private void OnConfigurationChanged(object? sender, EventArgs e) => this.Reload();

    /// <summary>Reads every installed app and its icon for the picker, the first time it is opened.</summary>
    private async Task LoadPickerAsync()
    {
        if (this.Picker is null || this.pickerLoaded)
        {
            return;
        }

        this.pickerLoaded = true;
        await this.Picker.LoadAsync().ConfigureAwait(true);
    }

    /// <summary>The icons of the apps on the list - only those, whatever else is installed.</summary>
    private async Task LoadListedIconsAsync()
    {
        if (this.installedApps is null || this.Apps.Count == 0)
        {
            return;
        }

        IReadOnlyList<Core.Applications.InstalledApp> installed = await this.installedApps.GetInstalledAppsAsync(CancellationToken.None).ConfigureAwait(true);
        foreach (IgnoredAppRowViewModel row in this.Apps.Where(row => !this.icons.ContainsKey(row.Name)).ToList())
        {
            string? path = installed.FirstOrDefault(app => string.Equals(app.Name, row.Name, StringComparison.OrdinalIgnoreCase))?.IconPath;
            this.icons[row.Name] = path is not null && File.Exists(path) ? TryLoad(path) : null;
        }

        this.Reload(force: true);
    }

    private static Bitmap? TryLoad(string path)
    {
        try
        {
            return new Bitmap(path);
        }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException)
        {
            // Not logged: an icon that cannot be read is left out.
            return null;
        }
    }

    private void Reload(bool force = false)
    {
        IReadOnlyList<string> ignored = this.appState.Configuration.IgnoredApps;
        if (!force && this.Apps.Select(row => row.Name).SequenceEqual(ignored, StringComparer.Ordinal))
        {
            return;
        }

        this.Apps.Clear();
        foreach (string name in ignored)
        {
            Bitmap? icon = this.icons.GetValueOrDefault(name) ?? this.Picker?.ResolveIcon(name);
            this.Apps.Add(new IgnoredAppRowViewModel(name, icon, row => _ = this.RemoveAsync(row)));
        }

        if (!force && this.Apps.Any(row => !this.icons.ContainsKey(row.Name)))
        {
            _ = this.LoadListedIconsAsync();
        }
    }
}

/// <summary>An app never touched, with its icon when known.</summary>
public sealed class IgnoredAppRowViewModel
{
    public IgnoredAppRowViewModel(string name, Bitmap? icon, Action<IgnoredAppRowViewModel> remove)
    {
        this.Name = name;
        this.Icon = icon;
        this.RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Name { get; }

    public Bitmap? Icon { get; }

    public bool HasIcon => this.Icon is not null;

    public RelayCommand RemoveCommand { get; }
}
