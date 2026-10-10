using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Backs the Stats page (agent.md section 11.1.2): the full-size balance chart with week/month
/// ranges and a per-context total breakdown, built on the same
/// <see cref="IAnalyticsService.GetDailyBalanceAsync"/> the Dashboard's mini chart uses.
/// </summary>
public sealed class StatsViewModel : ViewModelBase, IDisposable
{
    private const int WeekDays = 7;
    private const int MonthDays = 30;

    private readonly IAnalyticsService analyticsService;

    private int selectedRangeDays = WeekDays;
    private BalanceChartViewModel balanceChart = BalanceChartViewModel.Empty;

    public StatsViewModel(IAnalyticsService analyticsService)
    {
        this.analyticsService = analyticsService;

        this.SelectWeekCommand = new RelayCommand(() => this.SelectedRangeDays = WeekDays);
        this.SelectMonthCommand = new RelayCommand(() => this.SelectedRangeDays = MonthDays);

        AppHost.ConfigurationChanged += this.OnConfigurationChanged;

        _ = this.LoadAsync();
    }

    public int SelectedRangeDays
    {
        get => this.selectedRangeDays;
        private set
        {
            if (this.SetProperty(ref this.selectedRangeDays, value))
            {
                this.OnPropertyChanged(nameof(this.IsWeekSelected));
                this.OnPropertyChanged(nameof(this.IsMonthSelected));
                _ = this.LoadAsync();
            }
        }
    }

    public bool IsWeekSelected => this.SelectedRangeDays == WeekDays;

    public bool IsMonthSelected => this.SelectedRangeDays == MonthDays;

    /// <summary>The chosen range, drawn by the same BalanceChart template as the Dashboard's.</summary>
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

    public RelayCommand SelectWeekCommand { get; }

    public RelayCommand SelectMonthCommand { get; }

    public void Dispose()
    {
        AppHost.ConfigurationChanged -= this.OnConfigurationChanged;
    }

    private void OnConfigurationChanged(object? sender, EventArgs e) => _ = this.LoadAsync();

    private async Task LoadAsync()
    {
        IReadOnlyList<BalanceSummary> summaries = await this.analyticsService
            .GetDailyBalanceAsync(this.SelectedRangeDays, CancellationToken.None)
            .ConfigureAwait(true);

        // A month's thirty columns are labelled by day of month; a week's seven by weekday.
        string labelFormat = this.SelectedRangeDays > WeekDays ? "%d" : "ddd";
        this.BalanceChart = BalanceChartFactory.Build(summaries, AppHost.Configuration.Contexts, barAreaHeight: 200, labelFormat);
    }
}
