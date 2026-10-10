using System.Globalization;
using Avalonia.Media;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Turns <see cref="BalanceSummary"/> days into the balance chart shared by the Dashboard's 7-day
/// mini chart and the Stats page's longer one (agent.md section 11.1.2), so the two never drift in
/// how they draw the same data.
///
/// The chart is drawn with the app's own controls - one rounded column per day, stacked in each
/// profile's accent - rather than a charting library. The library's columns, axes and default font
/// looked bolted on next to everything else, and a popover needs none of what it offered: no zoom,
/// no axes to read values off. Hovering a day shows its exact hours instead.
/// </summary>
public static class BalanceChartFactory
{
    /// <param name="barAreaHeight">How tall the tallest day's column is drawn, in pixels.</param>
    /// <param name="labelFormat">How days are labelled: "ddd" for a week, "d" for a month.</param>
    public static BalanceChartViewModel Build(
        IReadOnlyList<BalanceSummary> summaries,
        IReadOnlyList<ContextDefinition> contexts,
        double barAreaHeight,
        string labelFormat)
    {
        // A month's columns sit closer together than a week's, so they are thinner.
        double barWidth = summaries.Count > 10 ? 10 : 18;

        // Brushes once per profile, not per segment; immutable, so safe wherever they are drawn.
        Dictionary<string, (string Name, IBrush Brush)> profiles = contexts.ToDictionary(
            context => context.Id,
            context => (context.DisplayName, AccentColorParser.ToBrush(context.AccentColor)));

        double tallestDay = summaries
            .Select(day => day.SecondsByContextId.Where(pair => profiles.ContainsKey(pair.Key)).Sum(pair => pair.Value))
            .DefaultIfEmpty(0)
            .Max();

        List<BalanceDayViewModel> days = [];
        for (int i = 0; i < summaries.Count; i++)
        {
            BalanceSummary day = summaries[i];

            // In profile order, so every column stacks its colors the same way up.
            List<(string Name, IBrush Brush, long Seconds)> parts = contexts
                .Select(context => (profiles[context.Id].Name, profiles[context.Id].Brush, day.SecondsByContextId.GetValueOrDefault(context.Id)))
                .Where(part => part.Item3 > 0)
                .ToList();

            // A StackPanel draws top-down; the first profile belongs at the bottom. Only the column's
            // outer ends are rounded, so the stack reads as one bar rather than separate pills.
            List<(IBrush Brush, long Seconds)> topDown = parts.Select(part => (part.Brush, part.Seconds)).Reverse().ToList();
            List<BalanceSegmentViewModel> segments = topDown
                .Select((part, index) => new BalanceSegmentViewModel(
                    part.Brush,
                    part.Seconds / tallestDay * barAreaHeight,
                    new Avalonia.CornerRadius(
                        index == 0 ? BarRadius : 0,
                        index == 0 ? BarRadius : 0,
                        index == topDown.Count - 1 ? BarRadius : 0,
                        index == topDown.Count - 1 ? BarRadius : 0)))
                .ToList();

            string date = day.Date.ToString("ddd d MMM", CultureInfo.CurrentCulture);
            string tooltip = parts.Count == 0
                ? $"{date}: nothing recorded"
                : $"{date}\n" + string.Join("\n", parts.Select(part => $"{part.Name}: {FormatHours(part.Seconds)}"));

            days.Add(new BalanceDayViewModel(
                day.Date.ToString(labelFormat, CultureInfo.CurrentCulture),
                isToday: i == summaries.Count - 1,
                segments,
                tooltip,
                barAreaHeight,
                barWidth));
        }

        return new BalanceChartViewModel(days, BuildContextTotals(summaries, contexts));
    }

    /// <summary>
    /// Sums each context's total hours across every day in <paramref name="summaries"/>, for the
    /// legend and the Stats page's breakdown. Contexts with zero total are omitted.
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

    private const double BarRadius = 4;

    /// <summary>"45m" under an hour, "2.5h" above.</summary>
    public static string FormatHours(long seconds) =>
        seconds < 3600 ? $"{Math.Max(1, seconds / 60)}m" : $"{seconds / 3600.0:0.#}h";
}

/// <summary>The whole chart: a column per day, and the legend of totals under it.</summary>
public sealed class BalanceChartViewModel(IReadOnlyList<BalanceDayViewModel> days, IReadOnlyList<ContextTotalViewModel> totals)
{
    public static BalanceChartViewModel Empty { get; } = new([], []);

    public IReadOnlyList<BalanceDayViewModel> Days { get; } = days;

    public IReadOnlyList<ContextTotalViewModel> Totals { get; } = totals;

    public bool HasData => this.Totals.Count > 0;
}

/// <summary>One day's column.</summary>
public sealed class BalanceDayViewModel(
    string label,
    bool isToday,
    IReadOnlyList<BalanceSegmentViewModel> segments,
    string tooltip,
    double barAreaHeight,
    double barWidth)
{
    public string Label { get; } = label;

    /// <summary>The last day of the range, labelled in bold so the week reads from left to now.</summary>
    public bool IsToday { get; } = isToday;

    /// <summary>Top to bottom, each a profile's share of the day in its accent.</summary>
    public IReadOnlyList<BalanceSegmentViewModel> Segments { get; } = segments;

    /// <summary>A nothing-recorded day still gets a stub, so the row of days never has holes.</summary>
    public bool IsEmpty => this.Segments.Count == 0;

    public string Tooltip { get; } = tooltip;

    /// <summary>How tall the tallest column in the chart is drawn; every column gets the same box.</summary>
    public double BarAreaHeight { get; } = barAreaHeight;

    public double BarWidth { get; } = barWidth;
}

/// <summary>One profile's part of a day's column.</summary>
public sealed class BalanceSegmentViewModel(IBrush brush, double height, Avalonia.CornerRadius cornerRadius)
{
    public IBrush Brush { get; } = brush;

    public double Height { get; } = height;

    /// <summary>Rounded only where the segment is the top or bottom of its column.</summary>
    public Avalonia.CornerRadius CornerRadius { get; } = cornerRadius;
}

/// <summary>
/// One context's total hours over a range, for the chart's legend and the Stats page.
/// </summary>
public sealed class ContextTotalViewModel(string displayName, IBrush accentBrush, double totalHours)
{
    public string DisplayName { get; } = displayName;

    public IBrush AccentBrush { get; } = accentBrush;

    public double TotalHours { get; } = totalHours;

    public string TotalHoursDisplay => BalanceChartFactory.FormatHours((long)(this.TotalHours * 3600));
}
