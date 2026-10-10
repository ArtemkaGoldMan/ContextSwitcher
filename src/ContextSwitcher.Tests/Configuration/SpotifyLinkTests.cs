using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Tests.Configuration;

public sealed class SpotifyLinkTests
{
    [Theory]
    [InlineData("https://open.spotify.com/playlist/37i9dQZF1DX0XUsuxWHRQd?si=abc123", "spotify:playlist:37i9dQZF1DX0XUsuxWHRQd")]
    [InlineData("  https://open.spotify.com/intl-de/album/4aawyAB9vmqN3uQ7FjRGTy  ", "spotify:album:4aawyAB9vmqN3uQ7FjRGTy")]
    [InlineData("http://open.spotify.com/track/11dFghVXANMlKmJXsNCbNl#x", "spotify:track:11dFghVXANMlKmJXsNCbNl")]
    [InlineData("https://open.spotify.com/Playlist/abc", "spotify:playlist:abc")]
    public void AShareLinkBecomesTheUriSpotifyPlays(string link, string uri)
    {
        Assert.Equal(uri, SpotifyLink.ToUri(link));
    }

    [Theory]
    [InlineData("spotify:playlist:37i9dQZF1DX0XUsuxWHRQd")]
    [InlineData("Morning Run")]
    [InlineData("https://example.com/playlist/abc")]
    [InlineData("")]
    public void AnythingElseIsLeftAlone(string value)
    {
        Assert.Equal(value, SpotifyLink.ToUri("  " + value + " "));
    }
}
