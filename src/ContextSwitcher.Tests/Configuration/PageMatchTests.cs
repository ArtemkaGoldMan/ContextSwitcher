using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Tests.Configuration;

public sealed class PageMatchTests
{
    [Theory]
    [InlineData("https://mail.google.com/", "https://mail.google.com/mail/u/0/#inbox")] // where Gmail redirects
    [InlineData("https://github.com/ArtemkaGoldMan", "https://github.com/ArtemkaGoldMan/ContextSwitcher/pulls")]
    [InlineData("https://github.com/ArtemkaGoldMan", "https://github.com/ArtemkaGoldMan")]
    [InlineData("https://github.com/ArtemkaGoldMan/", "https://github.com/ArtemkaGoldMan")]
    [InlineData("http://example.com/docs", "https://example.com/docs/intro")]        // http or https
    [InlineData("https://www.youtube.com/", "https://youtube.com/feed/subscriptions")] // www. or not
    [InlineData("https://Example.com/Docs", "https://example.com/docs")]              // letter case
    [InlineData("https://www.youtube.com/watch?v=abc", "https://www.youtube.com/watch?v=abc&t=42")]
    [InlineData("https://linear.app/inbox#top", "https://linear.app/inbox")]          // the # part
    public void ATabOnThePageOrUnderItMatches(string page, string tab)
    {
        Assert.True(PageMatch.Matches(page, tab));
    }

    [Theory]
    [InlineData("https://github.com/ArtemkaGoldMan", "https://github.com/ArtemkaGoldManX")]   // not under it, only alike
    [InlineData("https://github.com/ArtemkaGoldMan", "https://github.com/someone-else")]
    [InlineData("https://github.com/ArtemkaGoldMan", "https://github.com/")]                  // above it
    [InlineData("https://google.com/", "https://mail.google.com/")]                          // another site
    [InlineData("https://www.youtube.com/watch?v=abc", "https://www.youtube.com/watch?v=xyz")]
    [InlineData("https://example.com:8080/", "https://example.com/")]
    [InlineData("https://example.com/", "chrome://newtab/")]
    [InlineData("https://example.com/", "")]
    [InlineData("not a page", "https://example.com/")]
    public void AnythingElseDoesNot(string page, string tab)
    {
        Assert.False(PageMatch.Matches(page, tab));
    }
}
