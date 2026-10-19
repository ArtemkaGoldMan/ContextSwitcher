using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Logging;
using System.Reflection;
using Avalonia.Threading;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.App.Resources;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Whether a newer version is out, and installing it. One for the whole run: the Settings page shows
/// it, and the menu bar menu grows an "Update to …" entry while an update is waiting.
///
/// It checks GitHub shortly after launch and then once a day, unless that is turned off in Settings,
/// and on "Check now". It never installs anything by itself - an update restarts the app, and that is
/// for the person to choose. A check that fails on its own, offline say, says nothing: only one asked
/// for gets an error message.
///
/// A version found by the daily check is also announced in a macOS notification, once: the menu bar
/// entry is easy to miss, and is hidden altogether when macOS has no room for the icon.
/// </summary>
public sealed class UpdatesViewModel : ViewModelBase
{
    /// <summary>The notification's id: a newer version's notice replaces the older one.</summary>
    public const string NotificationId = "update";

    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    private readonly IAppState appState;
    private readonly IReleaseSource releaseSource;
    private readonly IAppUpdater updater;
    private readonly IProcessRunner processRunner;
    private readonly ISystemNotifications notifications;
    private readonly Func<DateTimeOffset> now;

    private ReleaseInfo? available;
    private Version? announced;
    private string? installBlocker;
    private string statusText = Strings.Updates_NotCheckedYet;
    private bool isBusy;
    private DispatcherTimer? timer;

    public UpdatesViewModel(
        IAppState appState,
        IReleaseSource releaseSource,
        IAppUpdater updater,
        IProcessRunner processRunner,
        ISystemNotifications notifications,
        Version currentVersion,
        Func<DateTimeOffset>? now = null)
    {
        this.appState = appState;
        this.releaseSource = releaseSource;
        this.updater = updater;
        this.processRunner = processRunner;
        this.notifications = notifications;
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
    public string CurrentVersionText => Localized.Format(Strings.Updates_Version, ReleaseVersion.Format(this.CurrentVersion));

    public string StatusText
    {
        get => this.statusText;
        private set => this.SetProperty(ref this.statusText, value);
    }

    public bool IsUpdateAvailable => this.available is not null;

    /// <summary>"0.2.0", or empty when there is no update.</summary>
    public string AvailableVersionText => this.available is null ? string.Empty : ReleaseVersion.Format(this.available.Version);

    /// <summary>What the waiting update brings, from its release's "What's new" list.</summary>
    public IReadOnlyList<string> AvailableNotes => this.available?.Notes ?? [];

    public bool HasNotes => this.AvailableNotes.Count > 0;

    /// <summary>"What's new in 0.4.1", over <see cref="AvailableNotes"/>.</summary>
    public string WhatsNewHeading => Localized.Format(Strings.Updates_WhatsNewHeading, this.AvailableVersionText);

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

    /// <summary>
    /// The link to the release: the download when this copy can't install it, the full notes under
    /// the list when there is one, and otherwise the notes themselves.
    /// </summary>
    public string ReleasePageLinkText => this.installBlocker is not null
        ? Localized.Format(Strings.Updates_DownloadFromGitHub, this.AvailableVersionText)
        : this.HasNotes
            ? Strings.Updates_ReleaseNotesLink
            : Localized.Format(Strings.Updates_WhatsNewIn, this.AvailableVersionText);

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
            if (this.appState.Configuration.CheckForUpdates)
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
            this.StatusText = Strings.Common_Checking;
        }

        try
        {
            ReleaseInfo? latest = await this.releaseSource.GetLatestAsync(CancellationToken.None).ConfigureAwait(true);
            AppLog.Note(
                "Updates",
                "CheckFinished",
                $"The latest release is {(latest is null ? "unknown" : ReleaseVersion.Format(latest.Version))}; this is {ReleaseVersion.Format(this.CurrentVersion)}.",
                new Dictionary<string, string> { ["userAsked"] = userAsked.ToString() });
            if (latest is null || latest.Version <= this.CurrentVersion)
            {
                this.SetAvailable(null);
                this.StatusText = Localized.Format(Strings.Updates_UpToDate, this.now());

                // A notice left from before this version was installed no longer holds.
                this.notifications.Remove(NotificationId);
                return;
            }

            this.InstallBlocker = await this.updater.GetInstallBlockerAsync(CancellationToken.None).ConfigureAwait(true) is { } blocker
                ? MessageText.Of(blocker)
                : null;
            this.SetAvailable(latest);
            this.StatusText = Localized.Format(Strings.Updates_Available, ReleaseVersion.Format(latest.Version));
            this.Announce(latest.Version, userAsked);
        }
        catch (UpdateException ex)
        {
            // The daily check says nothing to the person when it fails, so this is the only place it is said.
            AppLog.Warn(
                "Updates",
                "CheckFailed",
                $"Checking for an update failed: {ex.Detail.English}",
                data: new Dictionary<string, string> { ["key"] = ex.Detail.Key, ["userAsked"] = userAsked.ToString() });
            if (userAsked)
            {
                this.StatusText = Localized.Format(Strings.Updates_CouldNotCheck, MessageText.Of(ex.Detail));
            }
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    private async Task InstallAsync()
    {
        if (this.available is not { } found || this.IsBusy)
        {
            return;
        }

        this.IsBusy = true;
        ReleaseInfo release = await this.NewestAsync(found).ConfigureAwait(true);
        this.StatusText = Localized.Format(Strings.Updates_Downloading, ReleaseVersion.Format(release.Version));
        try
        {
            AppLog.Record("Updates", "UpdateStarted", $"Installing {ReleaseVersion.Format(release.Version)} over {ReleaseVersion.Format(this.CurrentVersion)}.");
            StagedUpdate staged = await this.updater.StageAsync(release, CancellationToken.None).ConfigureAwait(true);
            this.updater.InstallOnExit(staged);
            AppLog.Record("Updates", "UpdateStaged", $"{ReleaseVersion.Format(release.Version)} is ready; the app restarts to finish it.");
            this.StatusText = Strings.Updates_Restarting;
            this.RestartRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (UpdateException ex)
        {
            AppLog.Error(
                "Updates",
                "InstallFailed",
                $"Installing {ReleaseVersion.Format(release.Version)} failed: {ex.Detail.English}",
                data: new Dictionary<string, string> { ["key"] = ex.Detail.Key });
            this.StatusText = Localized.Format(Strings.Updates_CouldNotInstall, MessageText.Of(ex.Detail));
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    /// <summary>
    /// Each version once a run. Someone who clicked Check now is looking at the answer already; the
    /// daily check that finds the same version tomorrow would only repeat it.
    /// </summary>
    private void Announce(Version version, bool userAsked)
    {
        if (version == this.announced)
        {
            return;
        }

        this.announced = version;
        if (!userAsked)
        {
            this.notifications.Show(
                NotificationId,
                Localized.Format(Strings.Notification_UpdateTitle, ReleaseVersion.Format(version)),
                Strings.Notification_UpdateBody);
        }
    }

    /// <summary>
    /// The newest release, asked for again just before installing. The one the last check found can be
    /// a day old - with Check now hidden while it is offered - and installing it then found the next
    /// one straight after: 0.6.4 went in over 0.6.3 with 0.6.5 already out, and the app updated twice in
    /// a row. When GitHub can't be asked, the release already found is installed.
    /// </summary>
    private async Task<ReleaseInfo> NewestAsync(ReleaseInfo found)
    {
        try
        {
            if (await this.releaseSource.GetLatestAsync(CancellationToken.None).ConfigureAwait(true) is { } latest && latest.Version > found.Version)
            {
                AppLog.Note(
                    "Updates",
                    "NewerAtInstall",
                    $"{ReleaseVersion.Format(latest.Version)} came out since {ReleaseVersion.Format(found.Version)} was found; installing it instead.");
                this.SetAvailable(latest);
                return latest;
            }
        }
        catch (UpdateException ex)
        {
            AppLog.Note(
                "Updates",
                "RecheckFailed",
                $"Couldn't ask for a newer release before installing, so installing {ReleaseVersion.Format(found.Version)}: {ex.Detail.English}");
        }

        return found;
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
        this.OnPropertyChanged(nameof(this.AvailableNotes));
        this.OnPropertyChanged(nameof(this.HasNotes));
        this.OnPropertyChanged(nameof(this.WhatsNewHeading));
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
