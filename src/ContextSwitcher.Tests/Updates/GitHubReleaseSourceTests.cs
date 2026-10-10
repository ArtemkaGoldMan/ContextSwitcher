using System.Net;
using System.Text;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.Infrastructure.Updates;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Updates;

public sealed class GitHubReleaseSourceTests
{
    private const string Release = """
        {
          "tag_name": "v0.2.0",
          "html_url": "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.2.0",
          "draft": false,
          "prerelease": false,
          "assets": [
            { "name": "ContextSwitcher-0.2.0.dmg", "browser_download_url": "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/download/v0.2.0/ContextSwitcher-0.2.0.dmg" },
            { "name": "ContextSwitcher.zip", "browser_download_url": "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/download/v0.2.0/ContextSwitcher.zip" }
          ]
        }
        """;

    private readonly StubHttpHandler handler = new();

    [Fact]
    public async Task ReadsTheLatestReleasesVersionPageAndArchive()
    {
        this.handler.Respond = _ => Json(HttpStatusCode.OK, Release);

        ReleaseInfo? latest = await this.Source().GetLatestAsync(CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Equal(new Version(0, 2, 0), latest.Version);
        Assert.Equal("https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.2.0", latest.PageUrl);
        Assert.Equal("https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/download/v0.2.0/ContextSwitcher.zip", latest.ArchiveUrl);
    }

    /// <summary>GitHub's API turns away requests without a User-Agent, so every one names the app.</summary>
    [Fact]
    public async Task AsksGitHubsLatestReleaseEndpointAsTheApp()
    {
        this.handler.Respond = _ => Json(HttpStatusCode.OK, Release);

        await this.Source().GetLatestAsync(CancellationToken.None);

        HttpRequestMessage request = Assert.Single(this.handler.Requests);
        Assert.Equal("https://api.github.com/repos/ArtemkaGoldMan/ContextSwitcher/releases/latest", request.RequestUri!.ToString());
        Assert.Equal("ContextSwitcher/0.1.0", request.Headers.UserAgent.ToString());
    }

    /// <summary>Before anything is published GitHub answers 404 - that is "nothing new", not an error.</summary>
    [Fact]
    public async Task NoReleaseYetIsNotAnError()
    {
        this.handler.Respond = _ => Json(HttpStatusCode.NotFound, """{ "message": "Not Found" }""");

        Assert.Null(await this.Source().GetLatestAsync(CancellationToken.None));
    }

    /// <summary>A release published by hand with only a disk image is still offered, as a download.</summary>
    [Fact]
    public async Task AReleaseWithoutTheArchiveHasNoArchiveUrl()
    {
        this.handler.Respond = _ => Json(HttpStatusCode.OK, """{ "tag_name": "v0.2.0", "html_url": "https://github.com/x/y/releases/tag/v0.2.0", "assets": [] }""");

        ReleaseInfo? latest = await this.Source().GetLatestAsync(CancellationToken.None);

        Assert.Null(latest!.ArchiveUrl);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, """{ "message": "API rate limit exceeded" }""")]
    [InlineData(HttpStatusCode.OK, "<html>not json</html>")]
    [InlineData(HttpStatusCode.OK, """{ "tag_name": "nightly" }""")]
    public async Task AnAnswerThatCannotBeUsedIsAnUpdateError(HttpStatusCode status, string body)
    {
        this.handler.Respond = _ => Json(status, body);

        await Assert.ThrowsAsync<UpdateException>(() => this.Source().GetLatestAsync(CancellationToken.None));
    }

    [Fact]
    public async Task BeingOfflineIsAnUpdateError()
    {
        this.handler.Respond = _ => throw new HttpRequestException("No route to host");

        UpdateException error = await Assert.ThrowsAsync<UpdateException>(() => this.Source().GetLatestAsync(CancellationToken.None));
        Assert.Contains("internet", error.Message, StringComparison.Ordinal);
    }

    private GitHubReleaseSource Source() => new(new HttpClient(this.handler), "0.1.0");

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
