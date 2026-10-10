using System.Text.RegularExpressions;

namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Turns what Spotify's Share → Copy link gives you into the URI its AppleScript dictionary plays.
/// Spotify cannot list your playlists to a script, so a pasted link is the least typing there is -
/// but <c>play track</c> only accepts <c>spotify:playlist:…</c>, and the link is not that.
/// </summary>
public static partial class SpotifyLink
{
    /// <summary>
    /// <c>https://open.spotify.com/playlist/ID?si=…</c> (with or without an <c>/intl-xx</c> segment)
    /// becomes <c>spotify:playlist:ID</c>; anything else comes back trimmed and otherwise untouched.
    /// </summary>
    public static string ToUri(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string trimmed = value.Trim();
        Match match = OpenSpotifyLink().Match(trimmed);
        return match.Success
            ? $"spotify:{match.Groups["kind"].Value.ToLowerInvariant()}:{match.Groups["id"].Value}"
            : trimmed;
    }

    [GeneratedRegex(
        @"^https?://open\.spotify\.com/(?:intl-[a-z-]+/)?(?<kind>playlist|album|track|artist|episode|show)/(?<id>[A-Za-z0-9]+)(?:[/?#].*)?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex OpenSpotifyLink();
}
