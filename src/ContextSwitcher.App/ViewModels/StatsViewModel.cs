using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;

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
    private IReadOnlyList<ISeries> balanceSeries = [];
    private IReadOnlyList<ICartesianAxis> balanceXAxes = [new Axis()];
    private IReadOnlyList<ContextTotalViewModel> contextTotals = [];

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

    public IReadOnlyList<ISeries> BalanceSeries
    {
        get => this.balanceSeries;
        private set
        {
            if (this.SetProperty(ref this.balanceSeries, value))
            {
                this.OnPropertyChanged(nameof(this.HasBalanceData));
            }
        }
    }

    public IReadOnlyList<ICartesianAxis> BalanceXAxes
    {
        get => this.balanceXAxes;
        private set => this.SetProperty(ref this.balanceXAxes, value);
    }

    public bool HasBalanceData => this.BalanceSeries.Count > 0;

    public IReadOnlyList<ContextTotalViewModel> ContextTotals
    {
        get => this.contextTotals;
        private set => this.SetProperty(ref this.contextTotals, value);
    }

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

        string dateFormat = this.SelectedRangeDays > WeekDays ? "M/d" : "ddd";
        this.BalanceSeries = BalanceChartFactory.BuildSeries(summaries, AppHost.Configuration.Contexts);
        this.BalanceXAxes = BalanceChartFactory.BuildXAxes(summaries, dateFormat);
        this.ContextTotals = BalanceChartFactory.BuildContextTotals(summaries, AppHost.Configuration.Contexts);
    }
}
