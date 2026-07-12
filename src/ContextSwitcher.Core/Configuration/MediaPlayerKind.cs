namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Describes the media player that can be controlled for a context.
/// </summary>
public enum MediaPlayerKind
{
    /// <summary>
    /// Disables media automation for the context.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("None")]
    None,

    /// <summary>
    /// Uses Apple Music.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("AppleMusic")]
    AppleMusic,

    /// <summary>
    /// Uses Spotify.
    /// </summary>
    [System.Text.Json.Serialization.JsonStringEnumMemberName("Spotify")]
    Spotify
}
