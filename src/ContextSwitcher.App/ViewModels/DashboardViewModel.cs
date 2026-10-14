using Avalonia.Threading;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.App.Resources;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Backs the Dashboard popover: active context header with live elapsed time, per-context switch
/// buttons wired to the real switch pipeline, last-switch warnings, quick links, notes, and the
/// 7-day work-life balance chart.
/// </summary>
public sealed class DashboardViewModel : ViewModelBase, IDisposable
{
    private const int BalanceChartDays = 7;

    private readonly IContextSwitchService switchService;
    private readonly IAnalyticsService analyticsService;
    private readonly IJsonStore jsonStore;
    private readonly IProcessRunner processRunner;
    private readonly ConfigPaths configPaths;
    private readonly IClock clock;
    private readonly DispatcherTimer elapsedTimer;

    private string activeContextDisplayName = Strings.Dashboard_NoProfile;
    private string elapsedDisplay = Strings.Dashboard_CurrentProfile;
    private Avalonia.Media.IBrush activeAccentBrush = AccentColorParser.ToBrush(string.Empty);
    private string activeIcon = ProfileIcons.Fallback;
    private bool isSwitching;
    private string lastSwitchStatusText = string.Empty;
    private IReadOnlyList<string> lastSwitchWarnings = [];
    private IReadOnlyList<QuickLinkViewModel> quickLinks = [];
    private IReadOnlyList<string> notes = [];
    private DateTimeOffset? activeSince;
    private bool clockWanted;
    private bool isAnyActive;
    private BalanceChartViewModel balanceChart = BalanceChartViewModel.Empty;
    private IReadOnlyList<SwitchButtonViewModel> switchButtons = [];
    private IReadOnlyList<string> configurationWarnings = [];

    public DashboardViewModel(
        IContextSwitchService switchService,
        IAnalyticsService analyticsService,
        IJsonStore jsonStore,
        IProcessRunner processRunner,
        ConfigPaths configPaths,
        IClock clock)
    {
        this.switchService = switchService;
        this.analyticsService = analyticsService;
        this.jsonStore = jsonStore;
        this.processRunner = processRunner;
        this.configPaths = configPaths;
        this.clock = clock;

        // Not started here: the elapsed time is only worth keeping fresh while someone can see it.
        this.elapsedTimer = new DispatcherTimer();
        this.elapsedTimer.Tick += (_, _) => this.RefreshElapsedDisplay();

        this.SupportDeveloperCommand = new RelayCommand(() => this.OpenQuickLink(AppLinks.Support));
        this.OpenAppCommand = new RelayCommand(() => this.OpenAppRequested?.Invoke(this, EventArgs.Empty));
        this.CloseCommand = new RelayCommand(() => this.DismissRequested?.Invoke(this, EventArgs.Empty));

        this.RefreshFromConfiguration();
        this.ApplyState(AppHost.State);


        AppHost.ConfigurationChanged += this.OnConfigurationChanged;
        AppHost.StateChanged += this.OnStateChanged;

        _ = this.LoadBalanceChartAsync();
    }

    /// <summary>
    /// Raised when the footer's "Open App" button is pressed, so the view can open the Main App
    /// window (agent.md section 11.1.2) to the Profiles page.
    /// </summary>
    public event EventHandler? OpenAppRequested;

    /// <summary>Raised when the popover has done its job and should get out of the way.</summary>
    public event EventHandler? DismissRequested;

    public IReadOnlyList<SwitchButtonViewModel> SwitchButtons
    {
        get => this.switchButtons;
        private set => this.SetProperty(ref this.switchButtons, value);
    }

    public IReadOnlyList<string> ConfigurationWarnings
    {
        get => this.configurationWarnings;
        private set
        {
            if (this.SetProperty(ref this.configurationWarnings, value))
            {
                this.OnPropertyChanged(nameof(this.HasConfigurationWarnings));
            }
        }
    }

    public bool HasConfigurationWarnings => this.ConfigurationWarnings.Count > 0;

    public RelayCommand SupportDeveloperCommand { get; }

    public RelayCommand OpenAppCommand { get; }

    /// <summary>The popover's own close button.</summary>
    public RelayCommand CloseCommand { get; }

    public string ActiveContextDisplayName
    {
        get => this.activeContextDisplayName;
        private set => this.SetProperty(ref this.activeContextDisplayName, value);
    }

    /// <summary>Whether a profile is on - none is after the active one was clicked again.</summary>
    public bool IsAnyActive
    {
        get => this.isAnyActive;
        private set => this.SetProperty(ref this.isAnyActive, value);
    }

    /// <summary>
    /// "Active for 1h 20m" under the profile's name, "Current profile" when that isn't known, or,
    /// with every profile off, how to turn one on.
    /// </summary>
    public string ElapsedDisplay
    {
        get => this.elapsedDisplay;
        private set => this.SetProperty(ref this.elapsedDisplay, value);
    }

    /// <summary>The active profile's color, for the header badge.</summary>
    public Avalonia.Media.IBrush ActiveAccentBrush
    {
        get => this.activeAccentBrush;
        private set => this.SetProperty(ref this.activeAccentBrush, value);
    }

    /// <summary>The active profile's icon, drawn on <see cref="ActiveAccentBrush"/>.</summary>
    public string ActiveIcon
    {
        get => this.activeIcon;
        private set => this.SetProperty(ref this.activeIcon, value);
    }

    public bool IsSwitching
    {
        get => this.isSwitching;
        private set
        {
            if (this.SetProperty(ref this.isSwitching, value))
            {
                foreach (SwitchButtonViewModel button in this.SwitchButtons)
                {
                    ((AsyncRelayCommand)button.SwitchCommand).RaiseCanExecuteChanged();
                }
            }
        }
    }

    public string LastSwitchStatusText
    {
        get => this.lastSwitchStatusText;
        private set => this.SetProperty(ref this.lastSwitchStatusText, value);
    }

    public IReadOnlyList<string> LastSwitchWarnings
    {
        get => this.lastSwitchWarnings;
        private set
        {
            if (this.SetProperty(ref this.lastSwitchWarnings, value))
            {
                this.OnPropertyChanged(nameof(this.HasLastSwitchWarnings));
            }
        }
    }

    public bool HasLastSwitchWarnings => this.LastSwitchWarnings.Count > 0;

    public IReadOnlyList<QuickLinkViewModel> QuickLinks
    {
        get => this.quickLinks;
        private set
        {
            if (this.SetProperty(ref this.quickLinks, value))
            {
                this.OnPropertyChanged(nameof(this.HasQuickLinks));
            }
        }
    }

    public bool HasQuickLinks => this.QuickLinks.Count > 0;

    public IReadOnlyList<string> Notes
    {
        get => this.notes;
        private set
        {
            if (this.SetProperty(ref this.notes, value))
            {
                this.OnPropertyChanged(nameof(this.HasNotes));
            }
        }
    }

    public bool HasNotes => this.Notes.Count > 0;

    /// <summary>The last seven days, a column per day, drawn by the shared BalanceChart template.</summary>
    public BalanceChartViewModel BalanceChart
    {
        get => this.balanceChart;
        private set
        {
            if (this.SetProperty(ref this.balanceChart, value))
            {
                this.OnPropertyChanged(nameof(this.HasBalanceData));
            }
        }
    }

    public bool HasBalanceData => this.BalanceChart.HasData;

    /// <summary>Whether the elapsed time is being kept fresh - which it is only while the popover is showing.</summary>
    public bool IsClockRunning => this.elapsedTimer.IsEnabled;

    /// <summary>
    /// The popover is on screen: bring the elapsed time up to date and keep it so. Hidden, the popover
    /// stays in memory for the next click, and a clock ticking inside it would wake the app every
    /// second for text nobody can read.
    /// </summary>
    public void StartClock()
    {
        this.clockWanted = true;
        this.RefreshElapsedDisplay();
    }

    /// <summary>The popover is hidden: stop ticking until it is shown again.</summary>
    public void StopClock()
    {
        this.clockWanted = false;
        this.elapsedTimer.Stop();
    }

    /// <summary>
    /// How long until the elapsed text next reads differently: a second while it counts seconds, then
    /// until the minute turns over. Ticking every second for a text that changes once a minute was a
    /// wake-up in 59 of every 60.
    /// </summary>
    public static TimeSpan UntilElapsedTextChanges(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return TimeSpan.FromSeconds(1);
        }

        // A hair past the boundary, so the tick lands on the new minute rather than just before it.
        TimeSpan intoMinute = TimeSpan.FromTicks(elapsed.Ticks % TimeSpan.TicksPerMinute);
        return TimeSpan.FromMinutes(1) - intoMinute + TimeSpan.FromMilliseconds(50);
    }

    public void Dispose()
    {
        this.elapsedTimer.Stop();
        AppHost.ConfigurationChanged -= this.OnConfigurationChanged;
        AppHost.StateChanged -= this.OnStateChanged;
    }

    private void OnConfigurationChanged(object? sender, EventArgs e)
    {
        this.RefreshFromConfiguration();
        this.ApplyState(AppHost.State);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        this.ApplyState(AppHost.State);
        _ = this.LoadBalanceChartAsync();
    }

    private void RefreshFromConfiguration()
    {
        this.SwitchButtons = AppHost.Configuration.Contexts
            .Select(context => new SwitchButtonViewModel(
                context.Id,
                context.DisplayName,
                context.AccentColor,
                context.Icon,
                new AsyncRelayCommand(() => this.SwitchToAsync(context.Id), () => !this.IsSwitching)))
            .ToList();

        this.ConfigurationWarnings = AppHost.ConfigurationValidation.Errors
            .Select(MessageText.Of)
            .ToList();
    }

    private async Task SwitchToAsync(string contextId)
    {
        if (this.IsSwitching)
        {
            return;
        }

        this.IsSwitching = true;
        try
        {
            // The active profile's button turns it off.
            ContextSwitchResult result = await this.switchService
                .SwitchAsync(ContextSwitchRequest.ForClickOn(contextId, AppHost.State.CurrentContextId, ContextSwitchSource.Dashboard), CancellationToken.None)
                .ConfigureAwait(true);

            CurrentContextState? state = await this.jsonStore
                .ReadAsync<CurrentContextState>(this.configPaths.StatePath)
                .ConfigureAwait(true);
            AppHost.UpdateState(state ?? new CurrentContextState());

            // Behave like a menu: a clean switch closes the popover. Anything worth reading - a
            // warning, a failure, a switch rejected because another was running - keeps it open.
            if (result.Status is ContextSwitchStatus.Succeeded or ContextSwitchStatus.NoOp)
            {
                this.DismissRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            this.IsSwitching = false;
        }
    }

    private async Task LoadBalanceChartAsync()
    {
        IReadOnlyList<BalanceSummary> summaries = await this.analyticsService
            .GetDailyBalanceAsync(BalanceChartDays, CancellationToken.None)
            .ConfigureAwait(true);

        this.BalanceChart = BalanceChartFactory.Build(summaries, AppHost.Configuration.Contexts, barAreaHeight: 56, labelFormat: "ddd");
    }

    private void ApplyState(CurrentContextState state)
    {
        ContextDefinition? active = AppHost.Configuration.Contexts
            .FirstOrDefault(context => context.Id == state.CurrentContextId);

        this.ActiveContextDisplayName = active?.DisplayName ?? Strings.Dashboard_NoProfile;
        this.ActiveAccentBrush = AccentColorParser.ToBrush(active?.AccentColor ?? string.Empty);
        this.ActiveIcon = active?.Icon ?? ProfileIcons.Fallback;
        this.IsAnyActive = active is not null;

        // Read the last outcome from state rather than from the popover's own switch result: the
        // tray menu, the CLI and Shortcuts switch too, and their warnings belong here just as much.
        this.LastSwitchStatusText = DescribeStatus(state.LastSwitchStatus);
        this.LastSwitchWarnings = state.LastErrors.Select(MessageText.Of).ToList();
        // Since the last switch only while something is on: after turning off, that is how long
        // nothing has been.
        this.activeSince = active is null ? null : state.LastSwitchCompletedAt;
        this.RefreshElapsedDisplay();

        foreach (SwitchButtonViewModel button in this.SwitchButtons)
        {
            button.IsCurrent = button.ContextId == state.CurrentContextId;
        }

        this.QuickLinks = (active?.QuickLinks ?? [])
            .Select(link => new QuickLinkViewModel(link.Title, link.Url, new RelayCommand(() => this.OpenQuickLink(link.Url))))
            .ToList();

        this.Notes = active?.Notes ?? [];
    }

    /// <summary>The last switch's outcome in words; state.json keeps the enum name.</summary>
    private static string DescribeStatus(string? status) => status switch
    {
        nameof(ContextSwitchStatus.Succeeded) => Strings.Status_Succeeded,
        nameof(ContextSwitchStatus.SucceededWithWarnings) => Strings.Status_SucceededWithWarnings,
        nameof(ContextSwitchStatus.Failed) => Strings.Status_Failed,
        nameof(ContextSwitchStatus.Cancelled) => Strings.Status_Cancelled,
        nameof(ContextSwitchStatus.NoOp) => Strings.Status_NoOp,
        null or "" => string.Empty,
        _ => status
    };

    private void OpenQuickLink(string url)
    {
        _ = this.processRunner.RunAsync(new ProcessStartOptions("open", [url], TimeSpan.FromSeconds(5)), CancellationToken.None);
    }

    private void RefreshElapsedDisplay()
    {
        TimeSpan? elapsed = this.activeSince is { } since ? this.clock.UtcNow - since : null;
        this.ElapsedDisplay = elapsed is { } passed
            ? Localized.Format(Strings.Dashboard_ActiveFor, FormatElapsed(passed))
            : this.IsAnyActive ? Strings.Dashboard_CurrentProfile : Strings.Dashboard_PickAProfile;

        // The next tick is for the moment the text changes; with nothing counting, there is none.
        this.elapsedTimer.Stop();
        if (this.clockWanted && elapsed is { } counted)
        {
            this.elapsedTimer.Interval = UntilElapsedTextChanges(counted);
            this.elapsedTimer.Start();
        }
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalHours >= 1)
        {
            return Localized.Format(Strings.Units_HoursMinutes, (int)elapsed.TotalHours, elapsed.Minutes);
        }

        return elapsed.TotalMinutes >= 1
            ? Localized.Format(Strings.Units_Minutes, (int)elapsed.TotalMinutes)
            : Localized.Format(Strings.Units_Seconds, (int)elapsed.TotalSeconds);
    }
}
