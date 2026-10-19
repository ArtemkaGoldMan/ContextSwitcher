using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.App.Resources;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Settings' "Updates": this version, what the last check found, what a waiting update brings, and the
/// buttons to check now or install it. The checking, the installing and the notification are the
/// <see cref="UpdateChecker"/>'s; this puts what it knows into words, whoever started the check - the
/// daily one, Check now, or a click on the notification.
/// </summary>
public sealed class UpdatesViewModel : ViewModelBase
{
    private readonly UpdateChecker checker;
    private readonly IProcessRunner processRunner;

    public UpdatesViewModel(UpdateChecker checker, IProcessRunner processRunner)
    {
        this.checker = checker;
        this.processRunner = processRunner;

        this.CheckNowCommand = new AsyncRelayCommand(() => this.checker.CheckAsync(userAsked: true), () => !this.IsBusy);
        this.InstallCommand = new AsyncRelayCommand(this.checker.InstallAsync, () => !this.IsBusy && this.CanInstall);
        this.OpenReleasePageCommand = new RelayCommand(this.OpenReleasePage);

        // One for the whole run, as the checker is, so it never has to let go of it.
        this.checker.Changed += (_, _) => this.OnCheckerChanged();
    }

    /// <summary>"Version 0.1.0".</summary>
    public string CurrentVersionText => Localized.Format(Strings.Updates_Version, ReleaseVersion.Format(this.checker.CurrentVersion));

    public string StatusText => this.checker.Status switch
    {
        { Stage: UpdateStage.Checking } => Strings.Common_Checking,
        { Stage: UpdateStage.UpToDate, At: { } at } => Localized.Format(Strings.Updates_UpToDate, at.ToLocalTime()),
        { Stage: UpdateStage.Available, Version: { } version } => Localized.Format(Strings.Updates_Available, ReleaseVersion.Format(version)),
        { Stage: UpdateStage.CouldNotCheck, Problem: { } problem } => Localized.Format(Strings.Updates_CouldNotCheck, MessageText.Of(problem)),
        { Stage: UpdateStage.Downloading, Version: { } version } => Localized.Format(Strings.Updates_Downloading, ReleaseVersion.Format(version)),
        { Stage: UpdateStage.Restarting } => Strings.Updates_Restarting,
        { Stage: UpdateStage.CouldNotInstall, Problem: { } problem } => Localized.Format(Strings.Updates_CouldNotInstall, MessageText.Of(problem)),
        _ => Strings.Updates_NotCheckedYet
    };

    public bool IsUpdateAvailable => this.checker.Available is not null;

    /// <summary>"0.2.0", or empty when there is no update.</summary>
    public string AvailableVersionText => this.checker.Available is { } release ? ReleaseVersion.Format(release.Version) : string.Empty;

    /// <summary>What the waiting update brings, from its release's "What's new" list.</summary>
    public IReadOnlyList<string> AvailableNotes => this.checker.Available?.Notes ?? [];

    public bool HasNotes => this.AvailableNotes.Count > 0;

    /// <summary>"What's new in 0.4.1", over <see cref="AvailableNotes"/>.</summary>
    public string WhatsNewHeading => Localized.Format(Strings.Updates_WhatsNewHeading, this.AvailableVersionText);

    /// <summary>An update is waiting and this copy can put it in place itself.</summary>
    public bool CanInstall => this.checker.Available is not null && this.checker.InstallBlocker is null;

    /// <summary>Why a waiting update has to be downloaded by hand instead, or null.</summary>
    public string? InstallBlocker => this.checker.InstallBlocker is { } blocker ? MessageText.Of(blocker) : null;

    public bool HasInstallBlocker => this.IsUpdateAvailable && this.checker.InstallBlocker is not null;

    /// <summary>
    /// The link to the release: the download when this copy can't install it, the full notes under
    /// the list when there is one, and otherwise the notes themselves.
    /// </summary>
    public string ReleasePageLinkText => this.checker.InstallBlocker is not null
        ? Localized.Format(Strings.Updates_DownloadFromGitHub, this.AvailableVersionText)
        : this.HasNotes
            ? Strings.Updates_ReleaseNotesLink
            : Localized.Format(Strings.Updates_WhatsNewIn, this.AvailableVersionText);

    public bool IsBusy => this.checker.IsBusy;

    public AsyncRelayCommand CheckNowCommand { get; }

    public AsyncRelayCommand InstallCommand { get; }

    public RelayCommand OpenReleasePageCommand { get; }

    private void OnCheckerChanged()
    {
        this.OnPropertyChanged(nameof(this.StatusText));
        this.OnPropertyChanged(nameof(this.IsBusy));
        this.OnPropertyChanged(nameof(this.IsUpdateAvailable));
        this.OnPropertyChanged(nameof(this.AvailableVersionText));
        this.OnPropertyChanged(nameof(this.AvailableNotes));
        this.OnPropertyChanged(nameof(this.HasNotes));
        this.OnPropertyChanged(nameof(this.WhatsNewHeading));
        this.OnPropertyChanged(nameof(this.CanInstall));
        this.OnPropertyChanged(nameof(this.InstallBlocker));
        this.OnPropertyChanged(nameof(this.HasInstallBlocker));
        this.OnPropertyChanged(nameof(this.ReleasePageLinkText));
        this.CheckNowCommand.RaiseCanExecuteChanged();
        this.InstallCommand.RaiseCanExecuteChanged();
    }

    private void OpenReleasePage()
    {
        // Only ever a GitHub page: the address comes from the network, and `open` would open anything.
        if (this.checker.Available is { } release && release.PageUrl.StartsWith("https://github.com/", StringComparison.Ordinal))
        {
            _ = this.processRunner.RunAsync(new ProcessStartOptions("open", [release.PageUrl], TimeSpan.FromSeconds(5)), CancellationToken.None);
        }
    }
}
