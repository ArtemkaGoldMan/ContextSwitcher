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
}
