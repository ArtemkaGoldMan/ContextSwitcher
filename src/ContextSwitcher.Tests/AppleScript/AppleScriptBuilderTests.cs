using ContextSwitcher.Infrastructure.AppleScript;

namespace ContextSwitcher.Tests.AppleScript;

public sealed class AppleScriptBuilderTests
{
    [Fact]
    public void EscapeStringLiteralEscapesBackslashesAndQuotes()
    {
        string escaped = AppleScriptBuilder.EscapeStringLiteral("C:\\Users\\\"quoted\"\\file");

        Assert.Equal("C:\\\\Users\\\\\\\"quoted\\\"\\\\file", escaped);
    }

    [Fact]
    public void EscapeStringLiteralLeavesPlainTextUnchanged()
    {
        Assert.Equal("Visual Studio Code", AppleScriptBuilder.EscapeStringLiteral("Visual Studio Code"));
    }

    [Fact]
    public void QuitApplicationIfRunningEscapesAppName()
    {
        string script = AppleScriptBuilder.QuitApplicationIfRunning("My \"Cool\" App");

        Assert.Equal(
            "tell application \"My \\\"Cool\\\" App\" to if it is running then quit",
            script);
    }

    [Fact]
    public void IsApplicationRunningEscapesAppName()
    {
        string script = AppleScriptBuilder.IsApplicationRunning("Slack");

        Assert.Equal("application \"Slack\" is running", script);
    }

    /// <summary>
    /// The picker scripts must check before they tell: a <c>tell</c> block starts the app, and
    /// opening a picker should never launch the user's browser or Music.
    /// </summary>
    [Fact]
    public void ListOpenTabsChecksTheBrowserIsRunningBeforeTellingIt()
    {
        string script = AppleScriptBuilder.ListOpenTabs("My \"Odd\" Browser", isSafari: false);
        string[] lines = script.Split('\n');

        Assert.Equal("if application \"My \\\"Odd\\\" Browser\" is not running then return \"\"", lines[0]);
        Assert.True(
            Array.FindIndex(lines, line => line.StartsWith("tell application", StringComparison.Ordinal)) > 0,
            "the tell block comes before the running check");
        Assert.Contains("title of t", script, StringComparison.Ordinal);
        Assert.Contains("name of t", AppleScriptBuilder.ListOpenTabs("Safari", isSafari: true), StringComparison.Ordinal);
    }

    [Fact]
    public void ListMusicPlaylistsOnlyStartsMusicWhenAskedTo()
    {
        string guarded = AppleScriptBuilder.ListMusicPlaylists(startMusicIfNeeded: false);
        string starting = AppleScriptBuilder.ListMusicPlaylists(startMusicIfNeeded: true);

        Assert.StartsWith($"if application \"Music\" is not running then return \"{AppleScriptBuilder.MusicNotRunningMarker}\"", guarded, StringComparison.Ordinal);
        Assert.DoesNotContain("is not running", starting, StringComparison.Ordinal);
        Assert.Contains("name of every user playlist", starting, StringComparison.Ordinal);
    }
}
