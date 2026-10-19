using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Core.Automation;

/// <summary>The music a step starts.</summary>
/// <param name="Playlist">A playlist's name for Apple Music; a Spotify link or address for Spotify.</param>
/// <param name="AutoPlay">Whether it starts playing, or is only chosen.</param>
public sealed record MediaArguments(MediaPlayerKind Player, string Playlist, bool AutoPlay) : IStepArguments
{
    private const string PlayerKey = "player";
    private const string PlaylistKey = "playlist";
    private const string AutoPlayKey = "autoPlay";

    /// <summary>A player this build doesn't know is no player.</summary>
    public static MediaArguments From(IReadOnlyDictionary<string, string> arguments) => new(
        Enum.TryParse(arguments.GetValueOrDefault(PlayerKey), out MediaPlayerKind player) ? player : MediaPlayerKind.None,
        StepArgumentText.Text(arguments, PlaylistKey),
        StepArgumentText.Flag(arguments, AutoPlayKey));

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToDictionary() => new Dictionary<string, string>
    {
        [PlayerKey] = this.Player.ToString(),
        [PlaylistKey] = this.Playlist,
        [AutoPlayKey] = StepArgumentText.Flag(this.AutoPlay)
    };
}
