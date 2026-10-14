using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using ContextSwitcher.App.Resources;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Settings' list of apps ContextSwitcher never touches (<see cref="AppConfiguration.IgnoredApps"/>):
/// never opened, closed, hidden or asked about. Adding one takes it out of every profile, and says
/// which; removing one gives it back to no profile.
/// </summary>
public sealed class IgnoredAppsViewModel : ViewModelBase, IDisposable
{
    private readonly ConfigurationStore configurationStore;
    private string? message;

    public IgnoredAppsViewModel(ConfigurationStore configurationStore, IInstalledAppsService? installedApps)
    {
        this.configurationStore = configurationStore;
        this.Apps = new ObservableCollection<IgnoredAppRowViewModel>();
        if (installedApps is not null)
        {
            this.Picker = new AppPickerViewModel(installedApps, () => this.Apps.Select(row => row.Name), name => _ = this.AddAsync(name));
        }

        this.Reload();
        AppHost.ConfigurationChanged += this.OnConfigurationChanged;
        _ = this.LoadPickerAsync();
    }

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

    public void Dispose() => AppHost.ConfigurationChanged -= this.OnConfigurationChanged;

    /// <summary>Never touches <paramref name="name"/> from now on, and takes it out of every profile.</summary>
    public async Task AddAsync(string name)
    {
        (AppConfiguration updated, IReadOnlyList<string> takenOutOf) = IgnoredApps.Ignore(AppHost.Configuration, name);
        ConfigurationSaveResult result = await this.configurationStore.SaveAsync(updated, CancellationToken.None).ConfigureAwait(true);
        this.Message = result.Succeeded && takenOutOf.Count > 0
            ? Localized.Format(Strings.Settings_IgnoredApp_TakenOut, name, string.Join(", ", takenOutOf))
            : null;
        this.Reload();
    }

    private async Task RemoveAsync(IgnoredAppRowViewModel row)
    {
        await this.configurationStore.SaveAsync(IgnoredApps.StopIgnoring(AppHost.Configuration, row.Name), CancellationToken.None).ConfigureAwait(true);
        this.Message = null;
        this.Reload();
    }

    private void OnConfigurationChanged(object? sender, EventArgs e) => this.Reload();

    /// <summary>Reads the installed apps for the picker - and, with them, the listed apps' icons.</summary>
    private async Task LoadPickerAsync()
    {
        if (this.Picker is null)
        {
            return;
        }

        await this.Picker.LoadAsync().ConfigureAwait(true);
        this.Reload(force: true);
    }

    private void Reload(bool force = false)
    {
        IReadOnlyList<string> ignored = AppHost.Configuration.IgnoredApps;
        if (!force && this.Apps.Select(row => row.Name).SequenceEqual(ignored, StringComparer.Ordinal))
        {
            return;
        }

        this.Apps.Clear();
        foreach (string name in ignored)
        {
            this.Apps.Add(new IgnoredAppRowViewModel(name, this.Picker?.ResolveIcon(name), row => _ = this.RemoveAsync(row)));
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
