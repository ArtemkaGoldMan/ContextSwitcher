using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.Tests.Logging;

public sealed class LogRedactorTests
{
    [Theory]
    [InlineData("opened https://linear.app/team/x?token=abc#frag now", "opened https://linear.app/… now")]
    [InlineData("opened https://linear.app/ now", "opened https://linear.app/ now")]
    [InlineData("opened https://linear.app now", "opened https://linear.app now")]
    [InlineData("HTTPS://Github.com/pulls", "https://Github.com/…")]
    [InlineData("see https://me:secret@host.example/path", "see https://host.example/…")]
    [InlineData("go to http://localhost:3000/app?x=1", "go to http://localhost:3000/…")]
    public void AnAddressIsCutToItsSite(string text, string expected)
    {
        Assert.Equal(expected, LogRedactor.Redact(text));
    }

    [Theory]
    [InlineData("profile ann.lee+work@example.co.uk failed", "profile <email> failed")]
    [InlineData("Not authorized to send Apple events to Google Chrome. (-1743)", "Not authorized to send Apple events to Google Chrome. (-1743)")]
    public void AnEmailIsTakenOutAndOtherTextIsLeft(string text, string expected)
    {
        Assert.Equal(expected, LogRedactor.Redact(text));
    }

    [Theory]
    [InlineData("/Users/artem/Library/x.json missing", "~/Library/x.json missing")]
    [InlineData("nothing private here", "nothing private here")]
    [InlineData("", "")]
    public void TheHomeFolderIsReplaced(string text, string expected)
    {
        Assert.Equal(expected, LogRedactor.Redact(text));
    }

    [Fact]
    public void AnEntryIsCleanedInItsMessageAndItsData()
    {
        LogEntry entry = new()
        {
            Timestamp = DateTimeOffset.UnixEpoch,
            Level = LogLevel.Warning,
            Category = "Test",
            EventId = "E",
            Message = "see https://a.example/secret",
            Data = new Dictionary<string, string> { ["stderr"] = "/Users/artem/x", ["exitCode"] = "1" }
        };

        LogEntry clean = LogRedactor.Redact(entry);

        Assert.Equal("see https://a.example/…", clean.Message);
        Assert.Equal("~/x", clean.Data!["stderr"]);
        Assert.Equal("1", clean.Data["exitCode"]);
    }
}
