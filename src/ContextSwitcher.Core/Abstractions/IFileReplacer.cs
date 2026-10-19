namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Puts a newly written file in place of an old one in a single step, so a reader sees either the
/// old file or the new one and never half of either. Every save of the settings, the time records
/// and the log ends this way. How it copes with a file another process holds open differs from one
/// system to another, so the platform chooses the implementation.
/// </summary>
public interface IFileReplacer
{
    /// <summary>
    /// Moves <paramref name="source"/> over <paramref name="destination"/>, replacing it if it exists.
    /// </summary>
    /// <param name="source">The newly written file.</param>
    /// <param name="destination">The file it replaces.</param>
    /// <param name="cancellationToken">A token that can cancel the wait between tries.</param>
    Task ReplaceAsync(string source, string destination, CancellationToken cancellationToken = default);
}
