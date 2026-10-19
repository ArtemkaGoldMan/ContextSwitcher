using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Files;

/// <summary>
/// Replaces a file with <see cref="File.Move(string, string, bool)"/>, trying again after each of its
/// retry delays when the move is refused for a file held open elsewhere - which passes - and failing
/// at once for anything that doesn't, such as a file that isn't there. The delays are the whole
/// policy, so a system is given the one it needs: <see cref="Once"/> or
/// <see cref="RetryingWhileHeldOpen"/>.
/// </summary>
public sealed class FileReplacer : IFileReplacer
{
    private readonly IReadOnlyList<TimeSpan> retryDelays;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileReplacer"/> class.
    /// </summary>
    /// <param name="retryDelays">How long to wait before each try after the first; none, to try once.</param>
    public FileReplacer(IReadOnlyList<TimeSpan> retryDelays)
    {
        ArgumentNullException.ThrowIfNull(retryDelays);
        this.retryDelays = retryDelays;
    }

    /// <summary>
    /// Makes the move once. For macOS, which never refuses a move for an open file.
    /// </summary>
    public static FileReplacer Once { get; } = new([]);

    /// <summary>
    /// Tries again for under a second in all. For Windows, which refuses a move while another process
    /// has either file open without letting it be deleted - the app and the command line saving at
    /// once, an antivirus scanning what was just written, the search indexer - and they let go within
    /// moments.
    /// </summary>
    public static FileReplacer RetryingWhileHeldOpen { get; } = new(
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(400)
    ]);

    /// <inheritdoc />
    public async Task ReplaceAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < this.retryDelays.Count && IsHeldOpen(ex))
            {
                await Task.Delay(this.retryDelays[attempt], cancellationToken).ConfigureAwait(false);
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
