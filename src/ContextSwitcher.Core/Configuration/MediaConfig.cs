namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Configures music and media automation for a context.
/// </summary>
public sealed record MediaConfig
{
    /// <summary>
    /// Gets or sets the media player that should be controlled.
    /// </summary>
    public MediaPlayerKind Player { get; init; } = MediaPlayerKind.None;

    /// <summary>
    /// Gets or sets the playlist or track identifier to play.
    /// </summary>
    public string Playlist { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether media should auto-play when the context is activated.
    /// </summary>
    public bool AutoPlay { get; init; }
}
