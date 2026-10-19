using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>Starts the music a profile names, in the player it names.</summary>
public interface IMediaPlayer
{
    /// <summary>
    /// Plays <paramref name="playlist"/> in <paramref name="player"/>: a playlist's name for Apple
    /// Music, a <c>spotify:</c> address for Spotify. Returns what the system said, kept for the log;
    /// an exit code of 0 is success.
    /// </summary>
    Task<ProcessResult> PlayAsync(MediaPlayerKind player, string playlist, TimeSpan timeout, CancellationToken cancellationToken);
}
