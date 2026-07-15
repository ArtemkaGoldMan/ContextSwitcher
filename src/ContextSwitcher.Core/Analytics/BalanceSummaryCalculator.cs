namespace ContextSwitcher.Core.Analytics;

/// <summary>
/// Aggregates completed <see cref="ContextSession"/> records into daily per-context totals for the
/// dashboard's work-life balance chart.
/// </summary>
public static class BalanceSummaryCalculator
{
    /// <summary>
    /// Buckets completed sessions by the local calendar date they started on, summing duration per
    /// context, for every day in the inclusive <paramref name="from"/>-<paramref name="to"/> range.
    /// Days with no activity still appear with an empty totals dictionary.
    /// </summary>
    public static IReadOnlyList<BalanceSummary> ComputeDaily(IReadOnlyList<ContextSession> sessions, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        Dictionary<DateOnly, Dictionary<string, long>> buckets = [];
        for (DateOnly day = from; day <= to; day = day.AddDays(1))
        {
            buckets[day] = [];
        }

        foreach (ContextSession session in sessions)
        {
            if (session.DurationSeconds is not { } duration)
            {
                continue;
            }

            DateOnly day = DateOnly.FromDateTime(session.StartedAt.LocalDateTime);
            if (!buckets.TryGetValue(day, out Dictionary<string, long>? perContext))
            {
                continue;
            }

            perContext[session.ContextId] = perContext.GetValueOrDefault(session.ContextId) + duration;
        }

        return buckets
            .OrderBy(bucket => bucket.Key)
            .Select(bucket => new BalanceSummary(bucket.Key, bucket.Value))
            .ToList();
    }
}
