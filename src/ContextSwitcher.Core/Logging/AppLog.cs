using System.Collections.Concurrent;
using System.Globalization;
using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Core.Logging;

/// <summary>
/// The log for the places that cannot be handed an <see cref="ILogger"/>: a callback from macOS, a
/// static helper, a catch deep in a class that has nothing else to do with logging. The app sets it up
/// once at start (<see cref="Use"/>); before that, and in anything that has not, it does nothing - so a
/// test that meets a failure on purpose does not write a log.
///
/// A line that cannot be written never reaches the caller: logging a failure must not become another.
/// </summary>
public static class AppLog
{
    private const int StackFrames = 8;

    private static readonly ConcurrentDictionary<string, byte> ReportedApis = new(StringComparer.Ordinal);
    private static readonly AsyncLocal<ILogger?> Scoped = new();
    private static ILogger? shared;

    /// <summary>Sets the logger the app writes to. Done once, at start.</summary>
    public static void Use(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        shared = logger;
    }

    /// <summary>
    /// Writes to <paramref name="logger"/> for the code run from here on - and only that - until the
    /// result is disposed. For tests, which run side by side and so cannot share one.
    /// </summary>
    public static IDisposable UseForThisFlow(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        Scoped.Value = logger;
        return new Scope();
    }

    /// <summary>An entry that is only remembered, to be written if a problem follows.</summary>
    public static void Note(string category, string eventId, string message, IReadOnlyDictionary<string, string>? data = null) =>
        Write(LogLevel.Information, category, eventId, message, null, data);

    /// <summary>
    /// A rare event that is no problem but is worth finding later - an update about to be installed. Unlike
    /// <see cref="Note"/> it is written whatever happens.
    /// </summary>
    public static void Record(string category, string eventId, string message, IReadOnlyDictionary<string, string>? data = null) =>
        Write(LogLevel.Information, category, eventId, message, null, data, keep: true);

    /// <summary>A problem the app got past: something did not work, and nothing else was lost with it.</summary>
    public static void Warn(string category, string eventId, string message, Exception? exception = null, IReadOnlyDictionary<string, string>? data = null) =>
        Write(LogLevel.Warning, category, eventId, message, exception, data);

    /// <summary>A problem that cost the user something.</summary>
    public static void Error(string category, string eventId, string message, Exception? exception = null, IReadOnlyDictionary<string, string>? data = null) =>
        Write(LogLevel.Error, category, eventId, message, exception, data);

    /// <summary>The app is going down. Written before this returns, since nothing may come after it.</summary>
    public static void Critical(string category, string eventId, string message, Exception? exception = null) =>
        Write(LogLevel.Critical, category, eventId, message, exception, null);

    /// <summary>
    /// A macOS call that this Mac does not have, which the app does without. Said once a run for each
    /// place: it is how a Mac older or newer than the ones tested shows up, and it would be the same
    /// line every time the call was tried.
    /// </summary>
    public static void MissingApi(string place, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (ReportedApis.TryAdd(place, 0))
        {
            Warn("Platform", "MissingMacApi", $"{place} is not available on this Mac, so it was skipped: {exception.Message}", exception);
        }
    }

    private static void Write(LogLevel level, string category, string eventId, string message, Exception? exception, IReadOnlyDictionary<string, string>? data, bool keep = false) =>
        WriteTo(Scoped.Value ?? shared, level, category, eventId, message, exception, data, keep);

    /// <summary>
    /// Writes one entry to <paramref name="logger"/> before returning, and swallows a failure to: the
    /// way <see cref="LoggerExtensions"/> write too, so a class given a logger logs as this does.
    /// </summary>
    internal static void WriteTo(
        ILogger? logger, LogLevel level, string category, string eventId, string message, Exception? exception, IReadOnlyDictionary<string, string>? data, bool keep = false)
    {
        if (logger is null)
        {
            return;
        }

        try
        {
            logger.LogAsync(new LogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = level,
                Category = category,
                EventId = eventId,
                Message = message,
                Data = exception is null ? data : Describe(exception, data),
                Keep = keep
            }).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or ObjectDisposedException)
        {
            // Not logged: this is the log.
        }
    }

    /// <summary>What an exception says, for a line: its type, its message, and the top of where it came from.</summary>
    private static Dictionary<string, string> Describe(Exception exception, IReadOnlyDictionary<string, string>? data)
    {
        Dictionary<string, string> described = data is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(data, StringComparer.Ordinal);

        described["exception"] = exception.GetType().FullName ?? exception.GetType().Name;
        described["exceptionMessage"] = exception.Message;
        if (exception.StackTrace is { Length: > 0 } stack)
        {
            described["stack"] = string.Join('\n', stack.Split('\n').Take(StackFrames).Select(frame => frame.Trim()));
        }

        if (exception.InnerException is { } inner)
        {
            described["inner"] = $"{inner.GetType().FullName}: {inner.Message}";
        }

        return described;
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() => Scoped.Value = null;
    }
}
