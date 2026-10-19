using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>Starts the music the context switched to plays, when it plays any.</summary>
public sealed class ControlMediaHandler : IStepHandler
{
    private readonly IMediaPlayer player;
    private readonly IClock clock;

    public ControlMediaHandler(IMediaPlayer player, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(clock);

        this.player = player;
        this.clock = clock;
    }

    /// <inheritdoc />
    public AutomationStepType Type => AutomationStepType.ControlMedia;

    /// <inheritdoc />
    public async Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        DateTimeOffset startedAt = this.clock.UtcNow;
        string player = step.Arguments.GetValueOrDefault("player", nameof(MediaPlayerKind.None));
        string playlist = step.Arguments.GetValueOrDefault("playlist", string.Empty);
        bool autoPlay = step.Arguments.GetValueOrDefault("autoPlay") == "True";

        if (!autoPlay)
        {
            return StepResults.Succeeded(step, "Auto-play disabled; media not started.", startedAt, this.clock.UtcNow);
        }

        if (string.IsNullOrWhiteSpace(playlist))
        {
            UserMessage noPlaylist = SwitchMessages.NoPlaylist.With();
            return new AutomationResult(
                step.Id, step.Type, AutomationResultStatus.Warning, noPlaylist.English,
                null, null, null, startedAt, this.clock.UtcNow)
            {
                Problems = [noPlaylist]
            };
        }

        return player switch
        {
            nameof(MediaPlayerKind.AppleMusic) => await this.PlayAppleMusicAsync(step, playlist, startedAt, cancellationToken).ConfigureAwait(false),
            nameof(MediaPlayerKind.Spotify) => await this.PlaySpotifyAsync(step, SpotifyLink.ToUri(playlist), startedAt, cancellationToken).ConfigureAwait(false),
            _ => StepResults.Succeeded(step, "No media player configured.", startedAt, this.clock.UtcNow)
        };
    }

    private async Task<AutomationResult> PlayAppleMusicAsync(AutomationStep step, string playlist, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        ProcessResult result = await this.player
            .PlayAsync(MediaPlayerKind.AppleMusic, playlist, StepResults.WithReportingHeadroom(step.Timeout), cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset completedAt = this.clock.UtcNow;

        if (result.ExitCode == 0)
        {
            return StepResults.Succeeded(step, "Apple Music playlist started.", startedAt, completedAt);
        }

        return StepResults.WithProblem(step, SwitchMessages.CouldNotPlayAppleMusic.With(playlist), result, startedAt, completedAt);
    }

    private async Task<AutomationResult> PlaySpotifyAsync(AutomationStep step, string playlist, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        ProcessResult result = await this.player
            .PlayAsync(MediaPlayerKind.Spotify, playlist, StepResults.WithReportingHeadroom(step.Timeout), cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset completedAt = this.clock.UtcNow;

        if (result.ExitCode == 0)
        {
            return StepResults.Succeeded(step, "Spotify playback started.", startedAt, completedAt);
        }

        // Spotify automation failures are always non-critical (section 9.10), regardless of step.IsCritical.
        UserMessage spotify = SwitchMessages.CouldNotPlaySpotify.With(playlist);
        return new AutomationResult(
            step.Id, step.Type, AutomationResultStatus.Warning, spotify.English,
            result.ExitCode, result.StandardOutput, result.StandardError, startedAt, completedAt)
        {
            Problems = [spotify]
        };
    }
}
