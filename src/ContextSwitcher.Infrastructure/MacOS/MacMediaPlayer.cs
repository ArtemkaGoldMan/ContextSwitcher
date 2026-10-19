using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.AppleScript;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>Plays music in Music or Spotify through AppleScript.</summary>
public sealed class MacMediaPlayer : IMediaPlayer
{
    private readonly IScriptRunner scriptRunner;

    public MacMediaPlayer(IScriptRunner scriptRunner)
    {
        ArgumentNullException.ThrowIfNull(scriptRunner);

        this.scriptRunner = scriptRunner;
    }

    /// <inheritdoc />
    public Task<ProcessResult> PlayAsync(MediaPlayerKind player, string playlist, TimeSpan timeout, CancellationToken cancellationToken)
    {
        string script = player switch
        {
            MediaPlayerKind.AppleMusic => AppleScriptBuilder.PlayAppleMusicPlaylist(playlist),
            MediaPlayerKind.Spotify => AppleScriptBuilder.PlaySpotify(playlist),
            _ => throw new ArgumentOutOfRangeException(nameof(player), player, null)
        };
        return this.scriptRunner.RunAsync(script, timeout, cancellationToken);
    }
}
