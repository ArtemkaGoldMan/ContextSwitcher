using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.App.Startup;

/// <summary>Writes to the time history from a timer or an event, where nothing waits on the outcome.</summary>
internal static class TimeHistory
{
    /// <summary>Time tracking that can't write its file gives up on that once; it never takes the app down.</summary>
    public static async Task WriteQuietlyAsync(Func<Task> write, ILogger logger)
    {
        try
        {
            await write().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Warn("Analytics", "TimeNotRecorded", "The time history could not be written.", ex);
        }
    }
}
