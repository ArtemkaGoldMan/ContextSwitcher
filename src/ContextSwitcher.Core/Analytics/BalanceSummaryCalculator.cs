namespace ContextSwitcher.Core.Analytics;

/// <summary>
/// Aggregates completed <see cref="ContextSession"/> records into daily per-context totals for the
/// dashboard's work-life balance chart.
/// </summary>
public static class BalanceSummaryCalculator
{
    /// <summary>
    /// Sums each context's time per local calendar day, for every day in the inclusive
    /// <paramref name="from"/>-<paramref name="to"/> range. A session that runs past midnight is split
    /// at it: one from 22:00 to 02:00 is two hours on each day, where it used to count four on the
    /// day it started and none on the next. Days with no activity still appear with an empty totals
    /// dictionary.
    /// </summary>
    /// <param name="timeZone">Whose midnight counts; the Mac's own by default.</param>
    public static IReadOnlyList<BalanceSummary> ComputeDaily(
        IReadOnlyList<ContextSession> sessions,
        DateOnly from,
        DateOnly to,
        TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        TimeZoneInfo zone = timeZone ?? TimeZoneInfo.Local;

        Dictionary<DateOnly, Dictionary<string, long>> buckets = [];
        for (DateOnly day = from; day <= to; day = day.AddDays(1))
        {
            buckets[day] = [];
        }

        foreach (ContextSession session in sessions)
        {
            if (session.DurationSeconds is not { } duration || duration <= 0)
            {
                continue;
            }

            DateTimeOffset start = session.StartedAt;
            DateTimeOffset end = start.AddSeconds(duration);
            while (start < end)
            {
                DateTime local = TimeZoneInfo.ConvertTime(start, zone).DateTime;
                DateOnly day = DateOnly.FromDateTime(local);
                DateTimeOffset nextMidnight = StartOf(day.AddDays(1), zone);
                DateTimeOffset pieceEnd = nextMidnight < end ? nextMidnight : end;

                if (buckets.TryGetValue(day, out Dictionary<string, long>? perContext))
                {
                    long seconds = (long)Math.Round((pieceEnd - start).TotalSeconds);
                    perContext[session.ContextId] = perContext.GetValueOrDefault(session.ContextId) + seconds;
                }

                start = pieceEnd;
            }
        }

        return buckets
            .OrderBy(bucket => bucket.Key)
            .Select(bucket => new BalanceSummary(bucket.Key, bucket.Value))
            .ToList();
    }

    /// <summary>The first moment of <paramref name="day"/> in <paramref name="zone"/>.</summary>
    private static DateTimeOffset StartOf(DateOnly day, TimeZoneInfo zone)
    {
        DateTime midnight = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        // Where the clocks jump forward across midnight, the day starts at the first time that exists.
        while (zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddMinutes(30);
        }

        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));
    }
}
