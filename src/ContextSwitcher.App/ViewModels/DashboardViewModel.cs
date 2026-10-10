using Avalonia.Threading;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Files;

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

    private string activeContextDisplayName = "(none)";
    private string elapsedDisplay = "Current profile";
    private Avalonia.Media.IBrush activeAccentBrush = AccentColorParser.ToBrush(string.Empty);
    private string activeIcon = ProfileIcons.Fallback;
    private bool isSwitching;
    private string lastSwitchStatusText = string.Empty;
    private IReadOnlyList<string> lastSwitchWarnings = [];
    private IReadOnlyList<QuickLinkViewModel> quickLinks = [];
    private IReadOnlyList<string> notes = [];
    private DateTimeOffset? activeSince;
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

        this.SupportDeveloperCommand = new RelayCommand(() => this.OpenQuickLink(AppLinks.Support));
        this.OpenAppCommand = new RelayCommand(() => this.OpenAppRequested?.Invoke(this, EventArgs.Empty));
        this.CloseCommand = new RelayCommand(() => this.DismissRequested?.Invoke(this, EventArgs.Empty));

        this.RefreshFromConfiguration();
        this.ApplyState(AppHost.State);

        this.elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        this.elapsedTimer.Tick += (_, _) => this.RefreshElapsedDisplay();
        this.elapsedTimer.Start();

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

    /// <summary>"Active for 1h 20m" under the profile's name, or "Current profile" when unknown.</summary>
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
            .Select(error => $"{error.Path}: {error.Message}")
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
            ContextSwitchResult result = await this.switchService
                .SwitchAsync(new ContextSwitchRequest(contextId, ContextSwitchSource.Dashboard), CancellationToken.None)
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

        this.ActiveContextDisplayName = active?.DisplayName ?? "(none)";
        this.ActiveAccentBrush = AccentColorParser.ToBrush(active?.AccentColor ?? string.Empty);
        this.ActiveIcon = active?.Icon ?? ProfileIcons.Fallback;

        // Read the last outcome from state rather than from the popover's own switch result: the
        // tray menu, the CLI and Shortcuts switch too, and their warnings belong here just as much.
        this.LastSwitchStatusText = DescribeStatus(state.LastSwitchStatus);
        this.LastSwitchWarnings = state.LastErrors.Select(error => error.Message).ToList();
        this.activeSince = state.LastSwitchCompletedAt;
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
        nameof(ContextSwitchStatus.Succeeded) => "Switched",
        nameof(ContextSwitchStatus.SucceededWithWarnings) => "Switched, with warnings",
        nameof(ContextSwitchStatus.Failed) => "Switch failed",
        nameof(ContextSwitchStatus.Cancelled) => "Switch cancelled",
        nameof(ContextSwitchStatus.NoOp) => "Already there",
        null or "" => string.Empty,
        _ => status
    };

    private void OpenQuickLink(string url)
    {
        _ = this.processRunner.RunAsync(new ProcessStartOptions("open", [url], TimeSpan.FromSeconds(5)), CancellationToken.None);
    }

    private void RefreshElapsedDisplay()
    {
        this.ElapsedDisplay = this.activeSince is { } since
            ? $"Active for {FormatElapsed(this.clock.UtcNow - since)}"
            : "Current profile";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m";
        }

        return elapsed.TotalMinutes >= 1
            ? $"{(int)elapsed.TotalMinutes}m"
            : $"{(int)elapsed.TotalSeconds}s";
    }
}
