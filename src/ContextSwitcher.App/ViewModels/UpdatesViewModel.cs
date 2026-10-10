using System.Reflection;
using Avalonia.Threading;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Whether a newer version is out, and installing it. One for the whole run: the Settings page shows
/// it, and the menu bar menu grows an "Update to …" entry while an update is waiting.
///
/// It checks GitHub shortly after launch and then once a day, unless that is turned off in Settings,
/// and on "Check now". It never installs anything by itself - an update restarts the app, and that is
/// for the person to choose. A check that fails on its own, offline say, says nothing: only one asked
/// for gets an error message.
/// </summary>
public sealed class UpdatesViewModel : ViewModelBase
{
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    private readonly IReleaseSource releaseSource;
    private readonly IAppUpdater updater;
    private readonly IProcessRunner processRunner;
    private readonly Func<DateTimeOffset> now;

    private ReleaseInfo? available;
    private string? installBlocker;
    private string statusText = "Not checked yet.";
    private bool isBusy;
    private DispatcherTimer? timer;

    public UpdatesViewModel(IReleaseSource releaseSource, IAppUpdater updater, IProcessRunner processRunner, Version currentVersion, Func<DateTimeOffset>? now = null)
    {
        this.releaseSource = releaseSource;
        this.updater = updater;
        this.processRunner = processRunner;
        this.CurrentVersion = currentVersion;
        this.now = now ?? (() => DateTimeOffset.Now);

        this.CheckNowCommand = new AsyncRelayCommand(() => this.CheckAsync(userAsked: true), () => !this.IsBusy);
        this.InstallCommand = new AsyncRelayCommand(this.InstallAsync, () => !this.IsBusy && this.CanInstall);
        this.OpenReleasePageCommand = new RelayCommand(this.OpenReleasePage);
    }

    /// <summary>Raised once an update is ready to replace this copy: the app quits, and the new one opens.</summary>
    public event EventHandler? RestartRequested;

    /// <summary>This build's version, from the assembly - the same number the bundle and the release carry.</summary>
    public static Version RunningVersion { get; } =
        ReleaseVersion.TryParse(typeof(UpdatesViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, out Version version)
            ? version
            : new Version(0, 0, 0);

    public Version CurrentVersion { get; }

    /// <summary>"Version 0.1.0".</summary>
    public string CurrentVersionText => $"Version {ReleaseVersion.Format(this.CurrentVersion)}";

    public string StatusText
    {
        get => this.statusText;
        private set => this.SetProperty(ref this.statusText, value);
    }

    public bool IsUpdateAvailable => this.available is not null;

    /// <summary>"0.2.0", or empty when there is no update.</summary>
    public string AvailableVersionText => this.available is null ? string.Empty : ReleaseVersion.Format(this.available.Version);

    /// <summary>An update is waiting and this copy can put it in place itself.</summary>
    public bool CanInstall => this.available is not null && this.installBlocker is null;

    /// <summary>Why a waiting update has to be downloaded by hand instead, or null.</summary>
    public string? InstallBlocker
    {
        get => this.installBlocker;
        private set
        {
            if (this.SetProperty(ref this.installBlocker, value))
            {
                this.OnPropertyChanged(nameof(this.HasInstallBlocker));
                this.OnPropertyChanged(nameof(this.CanInstall));
                this.OnPropertyChanged(nameof(this.ReleasePageLinkText));
                this.InstallCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasInstallBlocker => this.IsUpdateAvailable && this.installBlocker is not null;

    /// <summary>The link to the release: its notes when the app installs it, the download when it can't.</summary>
    public string ReleasePageLinkText => this.installBlocker is null
        ? $"What's new in {this.AvailableVersionText} ↗"
        : $"Download {this.AvailableVersionText} from GitHub ↗";

    public bool IsBusy
    {
        get => this.isBusy;
        private set
        {
            if (this.SetProperty(ref this.isBusy, value))
            {
                this.CheckNowCommand.RaiseCanExecuteChanged();
                this.InstallCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand CheckNowCommand { get; }

    public AsyncRelayCommand InstallCommand { get; }

    public RelayCommand OpenReleasePageCommand { get; }

    /// <summary>
    /// Starts the background checks: one shortly after launch - not at launch, when the app has
    /// better things to do - and then one a day. Each one first asks whether checks are still wanted.
    /// </summary>
    public void StartAutomaticChecks()
    {
        if (this.timer is not null)
        {
            return;
        }

        this.timer = new DispatcherTimer { Interval = FirstCheckDelay };
        this.timer.Tick += (_, _) =>
        {
            this.timer.Interval = CheckInterval;
            if (AppHost.Configuration.CheckForUpdates)
            {
                _ = this.CheckAsync(userAsked: false);
            }
        };
        this.timer.Start();
    }

    /// <summary>Asks GitHub for the latest release. Errors are shown only when <paramref name="userAsked"/>.</summary>
    public async Task CheckAsync(bool userAsked)
    {
        if (this.IsBusy)
        {
            return;
        }

        this.IsBusy = true;
        if (userAsked)
        {
            this.StatusText = "Checking…";
        }

        try
        {
            ReleaseInfo? latest = await this.releaseSource.GetLatestAsync(CancellationToken.None).ConfigureAwait(true);
            if (latest is null || latest.Version <= this.CurrentVersion)
            {
                this.SetAvailable(null);
                this.StatusText = $"Up to date. Checked at {this.now():HH:mm}.";
                return;
            }

            this.InstallBlocker = await this.updater.GetInstallBlockerAsync(CancellationToken.None).ConfigureAwait(true);
            this.SetAvailable(latest);
            this.StatusText = $"Version {ReleaseVersion.Format(latest.Version)} is available.";
        }
        catch (UpdateException ex)
        {
            if (userAsked)
            {
                this.StatusText = $"Couldn't check for updates. {ex.Message}";
            }
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    private async Task InstallAsync()
    {
        if (this.available is not { } release || this.IsBusy)
        {
            return;
        }

        this.IsBusy = true;
        this.StatusText = $"Downloading version {ReleaseVersion.Format(release.Version)}…";
        try
        {
            StagedUpdate staged = await this.updater.StageAsync(release, CancellationToken.None).ConfigureAwait(true);
            this.updater.InstallOnExit(staged);
            this.StatusText = "Restarting…";
            this.RestartRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (UpdateException ex)
        {
            this.StatusText = $"Couldn't install the update. {ex.Message}";
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    private void SetAvailable(ReleaseInfo? release)
    {
        if (release is null)
        {
            this.InstallBlocker = null;
        }

        this.available = release;
        this.OnPropertyChanged(nameof(this.IsUpdateAvailable));
        this.OnPropertyChanged(nameof(this.AvailableVersionText));
        this.OnPropertyChanged(nameof(this.CanInstall));
        this.OnPropertyChanged(nameof(this.HasInstallBlocker));
        this.OnPropertyChanged(nameof(this.ReleasePageLinkText));
        this.InstallCommand.RaiseCanExecuteChanged();
    }

    private void OpenReleasePage()
    {
        // Only ever a GitHub page: the address comes from the network, and `open` would open anything.
        if (this.available is { } release && release.PageUrl.StartsWith("https://github.com/", StringComparison.Ordinal))
        {
            _ = this.processRunner.RunAsync(new ProcessStartOptions("open", [release.PageUrl], TimeSpan.FromSeconds(5)), CancellationToken.None);
        }
    }
}
