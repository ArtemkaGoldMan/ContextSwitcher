using System.Net;
using System.Text;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.Infrastructure.Updates;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Updates;

/// <summary>
/// Which release a Mac is offered. Once Windows has releases of its own GitHub's "latest" may carry
/// no Mac file at all, so the source looks through the newest releases for the highest version with
/// this platform's files.
/// </summary>
public sealed class GitHubReleaseSourceTests
{
    private const string Release = """
        {
          "tag_name": "v0.2.0",
          "html_url": "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.2.0",
          "draft": false,
          "prerelease": false,
          "body": "## What's new\r\n\r\n- **Faster switching.** Apps open together.\r\n\r\n## Install\r\n\r\n- Paste this.",
          "assets": [
            { "name": "ContextSwitcher-0.2.0.dmg", "browser_download_url": "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/download/v0.2.0/ContextSwitcher-0.2.0.dmg" },
            { "name": "ContextSwitcher.zip", "browser_download_url": "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/download/v0.2.0/ContextSwitcher.zip" }
          ]
        }
        """;

    /// <summary>The Mac's files, as the composition root hands them over - given here, not taken from the updater.</summary>
    private static readonly ReleaseAssets MacFiles = new("ContextSwitcher.zip", ".dmg");

    private readonly StubHttpHandler handler = new();

    [Fact]
    public async Task ReadsTheNewestReleasesVersionPageAndArchive()
    {
        this.handler.Respond = _ => Json(HttpStatusCode.OK, $"[{Release}]");

        ReleaseInfo? latest = await this.Source().GetLatestAsync(CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Equal(new Version(0, 2, 0), latest.Version);
        Assert.Equal("https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.2.0", latest.PageUrl);
        Assert.Equal("https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/download/v0.2.0/ContextSwitcher.zip", latest.ArchiveUrl);
    }

    [Fact]
    public async Task ReadsWhatsNewFromTheDescription()
    {
        this.handler.Respond = _ => Json(HttpStatusCode.OK, $"[{Release}]");

        ReleaseInfo? release = await this.Source().GetLatestAsync(CancellationToken.None);

        Assert.Equal(["Faster switching. Apps open together."], release?.Notes);
    }

    /// <summary>GitHub's API turns away requests without a User-Agent, so every one names the app.</summary>
    [Fact]
    public async Task AsksGitHubForTheNewestReleasesAsTheApp()
    {
        this.handler.Respond = _ => Json(HttpStatusCode.OK, $"[{Release}]");

        await this.Source().GetLatestAsync(CancellationToken.None);

        HttpRequestMessage request = Assert.Single(this.handler.Requests);
        Assert.Equal(
            $"https://api.github.com/repos/ArtemkaGoldMan/ContextSwitcher/releases?per_page={GitHubReleaseSource.PageSize}",
            request.RequestUri!.ToString());
        Assert.Equal("ContextSwitcher/0.1.0", request.Headers.UserAgent.ToString());
    }

    /// <summary>A Windows release published after the Mac's one is not offered to the Mac - the reason this looks past "latest".</summary>
    [Fact]
    public async Task AReleaseWithOnlyAnotherPlatformsFilesIsPassedOver()
    {
        this.handler.Respond = _ => Json(HttpStatusCode.OK, $"[{WindowsOnly("v0.3.0")}, {Release}]");

        ReleaseInfo? latest = await this.Source().GetLatestAsync(CancellationToken.None);

        Assert.Equal(new Version(0, 2, 0), latest?.Version);
    }

    /// <summary>Another platform's release is passed over whatever its tag says, so its tag can't break the Mac's check.</summary>
    [Fact]
    public async Task AnotherPlatformsTagIsNotRead()
    {
        this.handler.Respond = _ => Json(HttpStatusCode.OK, $"[{WindowsOnly("nightly")}, {Release}]");

        ReleaseInfo? latest = await this.Source().GetLatestAsync(CancellationToken.None);

        Assert.Equal(new Version(0, 2, 0), latest?.Version);
    }

    [Fact]
    public async Task TheHighestVersionIsOfferedWhateverOrderGitHubListsThemIn()
    {
        this.handler.Respond = _ => Json(HttpStatusCode.OK, $"[{Release}, {Mac("v0.3.0")}, {Mac("v0.2.5")}]");

        ReleaseInfo? latest = await this.Source().GetLatestAsync(CancellationToken.None);

        Assert.Equal(new Version(0, 3, 0), latest?.Version);
    }

    [Fact]
    public async Task DraftsAndPreReleasesAreNotOffered()
    {
        this.handler.Respond = _ => Json(
            HttpStatusCode.OK,
            $"[{Mac("v0.4.0", prerelease: true)}, {Mac("v0.3.0", draft: true)}, {Release}]");

        ReleaseInfo? latest = await this.Source().GetLatestAsync(CancellationToken.None);

        Assert.Equal(new Version(0, 2, 0), latest?.Version);
    }

    /// <summary>A release published by hand with only a disk image is still the Mac's, and offered as a download.</summary>
    [Fact]
    public async Task AReleaseWithOnlyADiskImageHasNoArchiveUrl()
    {
        this.handler.Respond = _ => Json(
            HttpStatusCode.OK,
            """[{ "tag_name": "v0.2.0", "html_url": "https://github.com/x/y/releases/tag/v0.2.0", "assets": [{ "name": "ContextSwitcher-0.2.0.dmg", "browser_download_url": "https://github.com/x/y/ContextSwitcher-0.2.0.dmg" }] }]""");

        ReleaseInfo? latest = await this.Source().GetLatestAsync(CancellationToken.None);

        Assert.Equal(new Version(0, 2, 0), latest?.Version);
        Assert.Null(latest!.ArchiveUrl);
    }

    /// <summary>Before anything is published there is no release for this platform - "nothing new", not an error.</summary>
    [Theory]
    [InlineData(HttpStatusCode.OK, "[]")]
    [InlineData(HttpStatusCode.NotFound, """{ "message": "Not Found" }""")]
    public async Task NoReleaseYetIsNotAnError(HttpStatusCode status, string body)
    {
        this.handler.Respond = _ => Json(status, body);

        Assert.Null(await this.Source().GetLatestAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, """{ "message": "API rate limit exceeded" }""")]
    [InlineData(HttpStatusCode.OK, "<html>not json</html>")]
    [InlineData(HttpStatusCode.OK, """{ "tag_name": "v0.2.0" }""")]
    [InlineData(HttpStatusCode.OK, """[{ "tag_name": "nightly", "assets": [{ "name": "ContextSwitcher.zip" }] }]""")]
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

    private GitHubReleaseSource Source() => new(new HttpClient(this.handler), "0.1.0", MacFiles);

    private static string Mac(string tag, bool draft = false, bool prerelease = false) =>
        $$"""{ "tag_name": "{{tag}}", "draft": {{(draft ? "true" : "false")}}, "prerelease": {{(prerelease ? "true" : "false")}}, "assets": [{ "name": "ContextSwitcher.zip", "browser_download_url": "https://example.com/{{tag}}/ContextSwitcher.zip" }] }""";

    private static string WindowsOnly(string tag) =>
        $$"""{ "tag_name": "{{tag}}", "assets": [{ "name": "ContextSwitcher-win-Setup.exe", "browser_download_url": "https://example.com/{{tag}}/ContextSwitcher-win-Setup.exe" }] }""";

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
