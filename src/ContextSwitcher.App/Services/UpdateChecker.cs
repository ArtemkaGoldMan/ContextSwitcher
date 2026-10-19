using Avalonia.Threading;
using ContextSwitcher.App.Resources;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.App.Services;

/// <summary>
/// Whether a newer version is out, and putting it in place. One for the whole run: Settings shows what
/// it found, and the menu bar menu grows an "Update to …" entry while an update is waiting.
///
/// It checks GitHub shortly after launch and then once a day, unless that is turned off in Settings,
/// and whenever someone asks. It never installs anything by itself - an update restarts the app, and
/// that is for the person to choose. A check that fails on its own, offline say, only goes in the log:
/// only one someone asked for says so on the page.
///
/// A version found by the daily check is also announced in a notification, once: the menu bar entry is
/// easy to miss, and is hidden altogether when macOS has no room for the icon.
/// </summary>
public sealed class UpdateChecker : IHousekeeping
{
    /// <summary>The notification's id: a newer version's notice replaces the older one.</summary>
    public const string NotificationId = "update";

    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    private readonly IReleaseSource releaseSource;
    private readonly IAppUpdater updater;
    private readonly ISystemNotifications notifications;
    private readonly IAppState appState;
    private readonly IAppInfo appInfo;
    private readonly IClock clock;
    private readonly ILogger logger;

    private Version? announced;
    private DispatcherTimer? timer;

    public UpdateChecker(
        IReleaseSource releaseSource,
        IAppUpdater updater,
        ISystemNotifications notifications,
        IAppState appState,
        IAppInfo appInfo,
        IClock clock,
        ILogger logger)
    {
        this.releaseSource = releaseSource;
        this.updater = updater;
        this.notifications = notifications;
        this.appState = appState;
        this.appInfo = appInfo;
        this.clock = clock;
        this.logger = logger;
    }

    /// <summary>Raised when anything below changes: a check or an install starts, gets somewhere or ends.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised once an update is ready to replace this copy: the app quits, and the new one opens.</summary>
    public event EventHandler? RestartRequested;

    /// <summary>This build's version.</summary>
    public Version CurrentVersion => this.appInfo.Version;

    /// <summary>The newer release found, or null.</summary>
    public ReleaseInfo? Available { get; private set; }

    /// <summary>Why the waiting update has to be downloaded by hand instead, or null.</summary>
    public UserMessage? InstallBlocker { get; private set; }

    /// <summary>A check or an install is under way.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>What the last check or install came to, for the page to say.</summary>
    public UpdateStatus Status { get; private set; } = UpdateStatus.NotCheckedYet;

    /// <summary>
    /// Starts the background checks: one shortly after launch - not at launch, when the app has
    /// better things to do - and then one a day. Each one first asks whether checks are still wanted.
    /// </summary>
    public void Start()
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

    /// <summary>Asks GitHub for the latest release. A failure is shown only when <paramref name="userAsked"/>.</summary>
    public async Task CheckAsync(bool userAsked)
    {
        if (this.IsBusy)
        {
            return;
        }

        this.IsBusy = true;
        if (userAsked)
        {
            this.Status = new UpdateStatus(UpdateStage.Checking);
        }

        this.OnChanged();
        try
        {
            ReleaseInfo? latest = await this.releaseSource.GetLatestAsync(CancellationToken.None).ConfigureAwait(true);
            this.logger.Note(
                "Updates",
                "CheckFinished",
                $"The latest release is {(latest is null ? "unknown" : ReleaseVersion.Format(latest.Version))}; this is {ReleaseVersion.Format(this.CurrentVersion)}.",
                new Dictionary<string, string> { ["userAsked"] = userAsked.ToString() });
            if (latest is null || latest.Version <= this.CurrentVersion)
            {
                this.Available = null;
                this.InstallBlocker = null;
                this.Status = new UpdateStatus(UpdateStage.UpToDate, At: this.clock.UtcNow);

                // A notice left from before this version was installed no longer holds.
                this.notifications.Remove(NotificationId);
                return;
            }

            this.InstallBlocker = await this.updater.GetInstallBlockerAsync(CancellationToken.None).ConfigureAwait(true);
            this.Available = latest;
            this.Status = new UpdateStatus(UpdateStage.Available, latest.Version);
            this.Announce(latest.Version, userAsked);
        }
        catch (UpdateException ex)
        {
            // The daily check says nothing to the person when it fails, so this is the only place it is said.
            this.logger.Warn(
                "Updates",
                "CheckFailed",
                $"Checking for an update failed: {ex.Detail.English}",
                data: new Dictionary<string, string> { ["key"] = ex.Detail.Key, ["userAsked"] = userAsked.ToString() });
            if (userAsked)
            {
                this.Status = new UpdateStatus(UpdateStage.CouldNotCheck, Problem: ex.Detail);
            }
        }
        finally
        {
            this.IsBusy = false;
            this.OnChanged();
        }
    }

    /// <summary>Downloads the waiting update and has it put in place when the app quits, then asks for that.</summary>
    public async Task InstallAsync()
    {
        if (this.Available is not { } found || this.IsBusy)
        {
            return;
        }

        this.IsBusy = true;
        this.OnChanged();
        ReleaseInfo release = await this.NewestAsync(found).ConfigureAwait(true);
        this.Status = new UpdateStatus(UpdateStage.Downloading, release.Version);
        this.OnChanged();
        try
        {
            this.logger.Record("Updates", "UpdateStarted", $"Installing {ReleaseVersion.Format(release.Version)} over {ReleaseVersion.Format(this.CurrentVersion)}.");
            StagedUpdate staged = await this.updater.StageAsync(release, CancellationToken.None).ConfigureAwait(true);
            this.updater.InstallOnExit(staged);
            this.logger.Record("Updates", "UpdateStaged", $"{ReleaseVersion.Format(release.Version)} is ready; the app restarts to finish it.");
            this.Status = new UpdateStatus(UpdateStage.Restarting, release.Version);
            this.OnChanged();
            this.RestartRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (UpdateException ex)
        {
            this.logger.Error(
                "Updates",
                "InstallFailed",
                $"Installing {ReleaseVersion.Format(release.Version)} failed: {ex.Detail.English}",
                data: new Dictionary<string, string> { ["key"] = ex.Detail.Key });
            this.Status = new UpdateStatus(UpdateStage.CouldNotInstall, release.Version, Problem: ex.Detail);
        }
        finally
        {
            this.IsBusy = false;
            this.OnChanged();
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
                this.logger.Note(
                    "Updates",
                    "NewerAtInstall",
                    $"{ReleaseVersion.Format(latest.Version)} came out since {ReleaseVersion.Format(found.Version)} was found; installing it instead.");
                this.Available = latest;
                return latest;
            }
        }
        catch (UpdateException ex)
        {
            this.logger.Note(
                "Updates",
                "RecheckFailed",
                $"Couldn't ask for a newer release before installing, so installing {ReleaseVersion.Format(found.Version)}: {ex.Detail.English}");
        }

        return found;
    }

    private void OnChanged() => this.Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>How far a check or an install got.</summary>
public enum UpdateStage
{
    NotCheckedYet,
    Checking,
    UpToDate,
    Available,
    CouldNotCheck,
    Downloading,
    Restarting,
    CouldNotInstall
}

/// <summary>What the last check or install came to.</summary>
/// <param name="Stage">How far it got.</param>
/// <param name="Version">The release it is about: the one found, or the one being installed.</param>
/// <param name="At">When a check found the app up to date.</param>
/// <param name="Problem">Why a check or an install failed.</param>
public sealed record UpdateStatus(UpdateStage Stage, Version? Version = null, DateTimeOffset? At = null, UserMessage? Problem = null)
{
    /// <summary>Before the first check.</summary>
    public static UpdateStatus NotCheckedYet { get; } = new(UpdateStage.NotCheckedYet);
}
