using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.Infrastructure.Logging;

/// <summary>
/// Keeps the log quiet until something goes wrong, and then tells all of it.
///
/// What happens is remembered, in memory, as a trail of recent entries. A switch that goes well leaves
/// nothing on disk. A warning or an error writes itself - and with it the trail that led to it: the
/// whole of its own switch, step by step with timings and what each step said, and the few entries
/// before that belong to nothing in particular, such as the start of the app. Reading the log then
/// shows what was done and where it went wrong, not a lone line saying that it did.
///
/// A crash is the case where the trail matters most, so the crash handlers write one too.
/// </summary>
public sealed class TrailLogger : ILogger
{
    /// <summary>How many entries are remembered; the oldest are forgotten first.</summary>
    public const int TrailCapacity = 300;

    /// <summary>How many recent entries that are not part of the problem's own switch are written with it.</summary>
    public const int StrayEntries = 20;

    /// <summary>How many switches that had a problem are remembered as such, so the rest of them is written.</summary>
    private const int TroubledSwitchesKept = 50;

    /// <summary>
    /// The same problem is written once in this long. One that repeats on every switch - a permission
    /// never given - says so once, then counts the rest, rather than filling the file.
    /// </summary>
    public static readonly TimeSpan RepeatWindow = TimeSpan.FromHours(1);

    private readonly LocalJsonLogger file;
    private readonly Func<RunInfo> runInfo;
    private readonly Func<bool> detailed;
    private readonly LogLevel? writeFrom;
    private readonly object gate = new();
    private readonly List<Crumb> trail = [];
    private readonly Dictionary<(string Category, string EventId, string Message), Repeat> repeats = [];
    private readonly Queue<string> troubled = new();
    private bool headerWritten;

    /// <param name="file">Where entries are written.</param>
    /// <param name="runInfo">Which app, on which Mac; asked for only when the first entry is written.</param>
    /// <param name="detailed">Whether everything is to be written, as it happens - for a week of looking into something.</param>
    /// <param name="writeFrom">A level from which entries are written as they happen, whatever <paramref name="detailed"/> says; null for the default, warnings.</param>
    public TrailLogger(LocalJsonLogger file, Func<RunInfo> runInfo, Func<bool>? detailed = null, LogLevel? writeFrom = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(runInfo);

        this.file = file;
        this.runInfo = runInfo;
        this.detailed = detailed ?? (() => false);
        this.writeFrom = writeFrom;
    }

    /// <inheritdoc />
    public async Task LogAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        LogEntry safe = LogRedactor.Redact(entry);
        IReadOnlyList<LogEntry> lines;

        lock (this.gate)
        {
            // After a problem in a switch, the rest of that switch is written as it happens: how it
            // ended - its status and how long it took - comes after the problem, not before.
            bool writeNow = safe.Keep
                || this.detailed()
                || (this.writeFrom is { } from && safe.Level >= from)
                || (!string.IsNullOrEmpty(safe.CorrelationId) && this.troubled.Contains(safe.CorrelationId));
            if (!writeNow && safe.Level < LogLevel.Warning)
            {
                this.Remember(safe);
                return;
            }

            safe = this.CountRepeats(safe, writeNow, out bool suppressed);
            if (suppressed)
            {
                return;
            }

            lines = this.Compose(safe, writeNow);
            if (safe.Level >= LogLevel.Warning && !string.IsNullOrEmpty(safe.CorrelationId) && !this.troubled.Contains(safe.CorrelationId))
            {
                this.troubled.Enqueue(safe.CorrelationId);
                while (this.troubled.Count > TroubledSwitchesKept)
                {
                    this.troubled.Dequeue();
                }
            }
        }

        await this.file.AppendAsync(lines, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The latest entries that are only in memory, not in the file, oldest first - what has happened
    /// that no problem has yet asked to be written. For a diagnostic report, which is made to be sent.
    /// </summary>
    public IReadOnlyList<LogEntry> Remembered(int most = 40)
    {
        lock (this.gate)
        {
            return this.trail.Where(crumb => !crumb.Written).TakeLast(most).Select(crumb => crumb.Entry).ToList();
        }
    }

    private void Remember(LogEntry entry)
    {
        this.trail.Add(new Crumb(entry));
        if (this.trail.Count > TrailCapacity)
        {
            this.trail.RemoveRange(0, this.trail.Count - TrailCapacity);
        }
    }

    /// <summary>
    /// A problem written less than <see cref="RepeatWindow"/> ago is only counted; the one that is
    /// written next says how many it stood in for.
    /// </summary>
    private LogEntry CountRepeats(LogEntry entry, bool writeNow, out bool suppressed)
    {
        suppressed = false;
        if (writeNow)
        {
            return entry;
        }

        (string, string, string) key = (entry.Category, entry.EventId, entry.Message);
        if (this.repeats.TryGetValue(key, out Repeat? seen) && entry.Timestamp - seen.LastWritten < RepeatWindow)
        {
            seen.Suppressed++;
            suppressed = true;
            return entry;
        }

        int stoodIn = seen?.Suppressed ?? 0;
        this.repeats[key] = new Repeat(entry.Timestamp);
        return stoodIn == 0 ? entry : WithData(entry, "repeated", stoodIn.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private List<LogEntry> Compose(LogEntry entry, bool writeNow)
    {
        List<LogEntry> lines = [];
        if (!this.headerWritten)
        {
            this.headerWritten = true;
            lines.Add(this.Header(entry.Timestamp));
        }

        if (!writeNow)
        {
            lines.AddRange(this.TakeTrailFor(entry).Select(crumb => WithData(crumb, "trail", "true")));
        }

        lines.Add(entry);
        return lines;
    }

    /// <summary>
    /// What led to <paramref name="entry"/>, oldest first, not written before. For a problem in a
    /// switch: all of that switch's entries, and the last few that belong to no switch at all - the
    /// start of the app, an update check. Another switch's entries are not part of its story. For
    /// a problem outside any switch, such as a crash: the last few entries of whatever kind, a switch
    /// that was under way included.
    /// </summary>
    private IEnumerable<LogEntry> TakeTrailFor(LogEntry entry)
    {
        bool inSwitch = !string.IsNullOrEmpty(entry.CorrelationId);
        List<Crumb> own = inSwitch
            ? this.trail.Where(crumb => !crumb.Written && crumb.Entry.CorrelationId == entry.CorrelationId).ToList()
            : [];
        List<Crumb> around = this.trail
            .Where(crumb => !crumb.Written
                && !own.Contains(crumb)
                && crumb.Entry.Timestamp <= entry.Timestamp
                && (!inSwitch || string.IsNullOrEmpty(crumb.Entry.CorrelationId)))
            .TakeLast(StrayEntries)
            .ToList();

        List<Crumb> taken = [.. own, .. around];
        foreach (Crumb crumb in taken)
        {
            crumb.Written = true;
        }

        return taken.OrderBy(crumb => crumb.Entry.Timestamp).Select(crumb => crumb.Entry);
    }

    private LogEntry Header(DateTimeOffset timestamp)
    {
        RunInfo info = this.runInfo();
        return new LogEntry
        {
            Timestamp = timestamp,
            Level = LogLevel.Information,
            Category = "Run",
            EventId = "RunStarted",
            Message = $"{info.AppName} {info.Version} on macOS {info.MacOs} ({info.Architecture})",
            Data = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["version"] = info.Version,
                ["macOS"] = info.MacOs,
                ["architecture"] = info.Architecture,
                ["build"] = info.Build,
                ["pid"] = info.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        };
    }

    private static LogEntry WithData(LogEntry entry, string key, string value)
    {
        Dictionary<string, string> data = entry.Data is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(entry.Data, StringComparer.Ordinal);
        data[key] = value;
        return entry with { Data = data };
    }

    private sealed class Crumb(LogEntry entry)
    {
        public LogEntry Entry { get; } = entry;

        public bool Written { get; set; }
    }

    private sealed class Repeat(DateTimeOffset lastWritten)
    {
        public DateTimeOffset LastWritten { get; } = lastWritten;

        public int Suppressed { get; set; }
    }
}
