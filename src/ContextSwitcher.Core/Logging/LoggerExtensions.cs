using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Core.Logging;

/// <summary>
/// The short forms of <see cref="AppLog"/> for a class that is given an <see cref="ILogger"/>: the
/// same levels, the same exception details, written before they return, and a line that can't be
/// written never reaches the caller. <see cref="AppLog"/> stays for what can't be given one.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>An entry that is only remembered, to be written if a problem follows.</summary>
    public static void Note(this ILogger logger, string category, string eventId, string message, IReadOnlyDictionary<string, string>? data = null) =>
        AppLog.WriteTo(logger, LogLevel.Information, category, eventId, message, null, data);

    /// <summary>A rare event that is no problem but is worth finding later; written whatever happens.</summary>
    public static void Record(this ILogger logger, string category, string eventId, string message, IReadOnlyDictionary<string, string>? data = null) =>
        AppLog.WriteTo(logger, LogLevel.Information, category, eventId, message, null, data, keep: true);

    /// <summary>A problem the app got past: something did not work, and nothing else was lost with it.</summary>
    public static void Warn(this ILogger logger, string category, string eventId, string message, Exception? exception = null, IReadOnlyDictionary<string, string>? data = null) =>
        AppLog.WriteTo(logger, LogLevel.Warning, category, eventId, message, exception, data);

    /// <summary>A problem that cost the user something.</summary>
    public static void Error(this ILogger logger, string category, string eventId, string message, Exception? exception = null, IReadOnlyDictionary<string, string>? data = null) =>
        AppLog.WriteTo(logger, LogLevel.Error, category, eventId, message, exception, data);
}
