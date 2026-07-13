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

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void SetDarkModeProducesExpectedScript(bool enabled, string expectedValue)
    {
        string script = AppleScriptBuilder.SetDarkMode(enabled);

        Assert.Equal(
            $"tell application \"System Events\" to tell appearance preferences to set dark mode to {expectedValue}",
            script);
    }

    [Theory]
    [InlineData(true, "every desktop")]
    [InlineData(false, "desktop 1")]
    public void SetWallpaperTargetsAllSpacesOrCurrentDesktop(bool allSpaces, string expectedTarget)
    {
        string script = AppleScriptBuilder.SetWallpaper("/tmp/wallpaper.jpg", allSpaces);

        Assert.Equal(
            $"tell application \"System Events\" to tell {expectedTarget} to set picture to \"/tmp/wallpaper.jpg\"",
            script);
    }

    [Fact]
    public void SetWallpaperEscapesPath()
    {
        string script = AppleScriptBuilder.SetWallpaper("/tmp/\"weird\" path.jpg", allSpaces: true);

        Assert.Equal(
            "tell application \"System Events\" to tell every desktop to set picture to \"/tmp/\\\"weird\\\" path.jpg\"",
            script);
    }
}
