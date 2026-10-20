using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>Spotify on Windows comes in stage 3; until then a profile has no music there, and nothing plays.</summary>
public sealed class NotYetMediaPlayer : IMediaPlayer
{
    /// <inheritdoc />
    public Task<ProcessResult> PlayAsync(MediaPlayerKind player, string playlist, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromResult(new ProcessResult(-1, string.Empty, NotYetReason.Music, TimedOut: false));
}
