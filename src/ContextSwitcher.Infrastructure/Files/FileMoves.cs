namespace ContextSwitcher.Infrastructure.Files;

/// <summary>
/// Puts one file in place of another. On Windows that is refused while any other process has either
/// file open without letting it be deleted - the app and the command line saving at once, an
/// antivirus scanning what was just written, the search indexer. They let go within moments, so on
/// Windows a move refused like that is tried again a few times before the failure stands. macOS never
/// refuses a move for an open file, so there it is made once, as it always was.
/// </summary>
internal static class FileMoves
{
    /// <summary>The waits before each try after the first, on Windows: under a second in all.</summary>
    private static readonly TimeSpan[] WindowsRetryDelays =
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(400)
    ];

    /// <summary>Moves <paramref name="source"/> over <paramref name="destination"/>, replacing it.</summary>
    public static Task ReplaceAsync(string source, string destination, CancellationToken cancellationToken = default) =>
        ReplaceAsync(source, destination, OperatingSystem.IsWindows() ? WindowsRetryDelays : [], cancellationToken);

    /// <summary>The same, waiting <paramref name="retryDelays"/> between tries - for the tests.</summary>
    internal static async Task ReplaceAsync(
        string source, string destination, IReadOnlyList<TimeSpan> retryDelays, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < retryDelays.Count && IsHeldOpen(ex))
            {
                await Task.Delay(retryDelays[attempt], cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Whether the move was refused for a file held open elsewhere, which passes, rather than for good:
    /// a file or folder that isn't there doesn't come back by waiting.
    /// </summary>
    private static bool IsHeldOpen(Exception ex) =>
        ex is UnauthorizedAccessException
        || (ex is IOException and not FileNotFoundException and not DirectoryNotFoundException and not PathTooLongException);
}
