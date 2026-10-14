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


    /// <summary>Closing never starts a browser that isn't running, and closes from the last tab back.</summary>
    [Fact]
    public void CloseTabsWithUrlsLeavesABrowserThatIsntRunningAndEscapesTheAddresses()
    {
        string script = AppleScriptBuilder.CloseTabsWithUrls("Google Chrome", ["https://a.example/", "https://b.example/?q=\"x\""]);

        Assert.StartsWith("if application \"Google Chrome\" is not running then return 0", script, StringComparison.Ordinal);
        Assert.Contains("set doomed to {\"https://a.example/\", \"https://b.example/?q=\\\"x\\\"\"}", script, StringComparison.Ordinal);
        Assert.Contains("to 1 by -1", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// Chrome's tabs are listed with their ids - which they keep wherever they navigate - and closed
    /// by them; neither script starts a browser that isn't running unless asked to.
    /// </summary>
    [Fact]
    public void ChromiumTabsAreListedAndClosedByTheirIds()
    {
        string guarded = AppleScriptBuilder.ListChromiumTabs("Brave Browser", startIfNeeded: false);
        string starting = AppleScriptBuilder.ListChromiumTabs("Brave Browser", startIfNeeded: true);
        string closing = AppleScriptBuilder.CloseChromiumTabsWithIds("Brave Browser", [7, 12]);

        Assert.StartsWith("if application \"Brave Browser\" is not running then return \"\"", guarded, StringComparison.Ordinal);
        Assert.DoesNotContain("is not running", starting, StringComparison.Ordinal);
        Assert.Contains("((id of t) as text) & separator & (URL of t)", starting, StringComparison.Ordinal);
        Assert.StartsWith("if application \"Brave Browser\" is not running then return 0", closing, StringComparison.Ordinal);
        Assert.Contains("set doomed to {\"7\", \"12\"}", closing, StringComparison.Ordinal);
        Assert.Contains("if doomed contains ((id of tab i of w) as text) then", closing, StringComparison.Ordinal);
    }

    /// <summary>
    /// Found by closing a real tab in Chrome: looping straight over <c>id of every window</c> hands
    /// Chrome "item 1 of id of every window", which it can't resolve, and the try skipped every
    /// window - no tab ever closed. Both closing scripts take the ids into a list first.
    /// </summary>
    [Fact]
    public void ClosingScriptsLoopOverAListOfWindowIds()
    {
        foreach (string script in new[]
        {
            AppleScriptBuilder.CloseChromiumTabsWithIds("Google Chrome", [7]),
            AppleScriptBuilder.CloseTabsWithUrls("Safari", ["https://a.example/"])
        })
        {
            Assert.Contains("set windowIds to id of every window\n    repeat with windowId in windowIds", script, StringComparison.Ordinal);
            Assert.DoesNotContain("in (id of every window)", script, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The tab scripts compile against Chrome's real dictionary - a misspelt term only shows up
    /// there. Compiling reads the dictionary from the app's bundle; nothing is run or started.
    /// </summary>
    [Fact]
    public async Task TheChromeTabScriptsCompile()
    {
        if (!OperatingSystem.IsMacOS() || !Directory.Exists("/Applications/Google Chrome.app"))
        {
            return;
        }

        foreach (string script in new[]
        {
            AppleScriptBuilder.ListChromiumTabs("Google Chrome", startIfNeeded: false),
            AppleScriptBuilder.ListChromiumTabs("Google Chrome", startIfNeeded: true),
            AppleScriptBuilder.CloseChromiumTabsWithIds("Google Chrome", [7, 12]),
            AppleScriptBuilder.CloseTabsWithUrls("Google Chrome", ["https://a.example/"]),
            AppleScriptBuilder.ListOpenTabs("Google Chrome", isSafari: false, skipIncognito: true)
        })
        {
            string source = Path.Combine(Path.GetTempPath(), $"cs-{Guid.NewGuid():N}.applescript");
            string compiled = Path.ChangeExtension(source, ".scpt");
            await File.WriteAllTextAsync(source, script);
            try
            {
                using System.Diagnostics.Process osacompile = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo("osacompile", ["-o", compiled, source]) { RedirectStandardError = true })!;
                string errors = await osacompile.StandardError.ReadToEndAsync();
                await osacompile.WaitForExitAsync();

                Assert.True(osacompile.ExitCode == 0, $"didn't compile: {errors}\n{script}");
            }
            finally
            {
                File.Delete(source);
                File.Delete(compiled);
            }
        }
    }

    /// <summary>Asking about everything else open leaves Chrome's incognito windows out; Safari can't say which windows are private.</summary>
    [Fact]
    public void IncognitoWindowsCanBeLeftOut()
    {
        Assert.Contains("(mode of w) is not \"incognito\"", AppleScriptBuilder.ListOpenTabs("Google Chrome", isSafari: false, skipIncognito: true), StringComparison.Ordinal);
        Assert.DoesNotContain("incognito", AppleScriptBuilder.ListOpenTabs("Google Chrome", isSafari: false), StringComparison.Ordinal);
        Assert.DoesNotContain("incognito", AppleScriptBuilder.ListOpenTabs("Safari", isSafari: true, skipIncognito: true), StringComparison.Ordinal);
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
