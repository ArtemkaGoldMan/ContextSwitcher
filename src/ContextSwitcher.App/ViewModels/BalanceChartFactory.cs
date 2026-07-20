using Avalonia.Media;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Configuration;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Builds LiveCharts2 series and axes from <see cref="BalanceSummary"/> data, shared by the
/// Dashboard's 7-day mini chart and the Stats page's longer-range chart (agent.md section
/// 11.1.2) so the two never drift in how they render the same underlying data.
/// </summary>
public static class BalanceChartFactory
{
    public static IReadOnlyList<ISeries> BuildSeries(IReadOnlyList<BalanceSummary> summaries, IReadOnlyList<ContextDefinition> contexts)
    {
        List<ISeries> series = [];
        foreach (ContextDefinition context in contexts)
        {
            double[] hoursPerDay = summaries
                .Select(day => day.SecondsByContextId.GetValueOrDefault(context.Id) / 3600.0)
                .ToArray();

            if (Array.TrueForAll(hoursPerDay, hours => hours == 0))
            {
                continue;
            }

            Color accent = Color.TryParse(context.AccentColor, out Color parsed) ? parsed : Colors.Gray;
            series.Add(new StackedColumnSeries<double>
            {
                Name = context.DisplayName,
                Values = hoursPerDay,
                Fill = new SolidColorPaint(new SKColor(accent.R, accent.G, accent.B))
            });
        }

        return series;
    }

    public static IReadOnlyList<ICartesianAxis> BuildXAxes(IReadOnlyList<BalanceSummary> summaries, string dateFormat = "ddd")
    {
        return [new Axis { Labels = summaries.Select(day => day.Date.ToString(dateFormat)).ToList() }];
    }

    /// <summary>
    /// Sums each context's total hours across every day in <paramref name="summaries"/>, for a
    /// per-context breakdown alongside the chart. Contexts with zero total are omitted.
    /// </summary>
    public static IReadOnlyList<ContextTotalViewModel> BuildContextTotals(IReadOnlyList<BalanceSummary> summaries, IReadOnlyList<ContextDefinition> contexts)
    {
        List<ContextTotalViewModel> totals = [];
        foreach (ContextDefinition context in contexts)
        {
            double totalHours = summaries.Sum(day => day.SecondsByContextId.GetValueOrDefault(context.Id) / 3600.0);
            if (totalHours <= 0)
            {
                continue;
            }

            totals.Add(new ContextTotalViewModel(context.DisplayName, AccentColorParser.ToBrush(context.AccentColor), totalHours));
        }

        return totals;
    }
}

/// <summary>
/// One context's total hours over a Stats page date range.
/// </summary>
public sealed class ContextTotalViewModel(string displayName, IBrush accentBrush, double totalHours)
{
    public string DisplayName { get; } = displayName;

    public IBrush AccentBrush { get; } = accentBrush;

    public double TotalHours { get; } = totalHours;

    public string TotalHoursDisplay => $"{this.TotalHours:0.#}h";
}
