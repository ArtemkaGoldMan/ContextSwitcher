using System.Net;
using System.Net.Http.Headers;
using ContextSwitcher.Infrastructure.Browser;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Browser;

/// <summary>
/// A tab's site icon comes from the site itself - its /favicon.ico, else the icon its home page names -
/// and is kept, as a 32×32 PNG made by the platform's image converter, so a site is asked once. No
/// network here, and no <c>sips</c>: the sites are stubbed, and the converter copies the file as it is.
/// </summary>
public sealed class SiteIconsTests : IDisposable
{
    /// <summary>A real 1×1 PNG, which the checks for a picture accept like any icon.</summary>
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly string directory = Path.Combine(Path.GetTempPath(), "cs-site-icons-" + Guid.NewGuid().ToString("N"));
    private readonly StubHttpHandler http = new();
    private readonly FakeImageConverter converter = new();

    [Fact]
    public async Task TheSitesFaviconIsKeptAndNotAskedForAgain()
    {
        this.http.Respond = request => request.RequestUri!.AbsolutePath == "/favicon.ico" ? Image(Png) : new HttpResponseMessage(HttpStatusCode.NotFound);
        SiteIcons icons = this.Create();

        string? path = await icons.GetIconPathAsync("https://www.github.com/pulls?q=open", CancellationToken.None);
        string? again = await icons.GetIconPathAsync("https://www.github.com/notifications", CancellationToken.None);

        Assert.NotNull(path);
        Assert.Equal(path, again);
        Assert.True(File.Exists(path));
        Assert.Equal(32, Assert.Single(this.converter.Calls).Size);
        Assert.Equal("https://www.github.com/favicon.ico", Assert.Single(this.http.Requests).RequestUri!.ToString());
    }

    [Fact]
    public async Task WithoutAFaviconTheIconTheHomePageNamesIsUsed()
    {
        this.http.Respond = request => request.RequestUri!.AbsolutePath switch
        {
            "/favicon.ico" => Page("<html>not here</html>"),
            "/" => Page("""<html><head><link rel="stylesheet" href="/s.css"><link rel="icon" type="image/png" href="/static/icon.png"></head></html>"""),
            "/static/icon.png" => Image(Png),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };

        string? path = await this.Create().GetIconPathAsync("https://docs.example/d/1", CancellationToken.None);

        Assert.NotNull(path);
        Assert.Equal(["/favicon.ico", "/", "/static/icon.png"], this.http.Requests.Select(request => request.RequestUri!.AbsolutePath));
    }

    /// <summary>A site with no icon, or one too big, is noted - and not asked again for a week.</summary>
    [Fact]
    public async Task ASiteWithNoIconIsNotAskedAgain()
    {
        this.http.Respond = request => request.RequestUri!.AbsolutePath == "/favicon.ico"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[200 * 1024]) { Headers = { ContentType = new MediaTypeHeaderValue("image/x-icon") } } }
            : new HttpResponseMessage(HttpStatusCode.NotFound);
        SiteIcons icons = this.Create();

        Assert.Null(await icons.GetIconPathAsync("https://big.example/", CancellationToken.None));
        int asked = this.http.Requests.Count;
        Assert.Null(await icons.GetIconPathAsync("https://big.example/other", CancellationToken.None));

        Assert.Equal(asked, this.http.Requests.Count);
    }

    [Fact]
    public async Task OnlyWebPagesHaveSites()
    {
        Assert.Null(await this.Create().GetIconPathAsync("chrome://newtab/", CancellationToken.None));
        Assert.Empty(this.http.Requests);
    }

    [Theory]
    [InlineData("""<link rel="shortcut icon" href="/favicon-32.png">""", "https://site.example/favicon-32.png")]
    [InlineData("""<LINK REL='icon' HREF='img/i.ico'>""", "https://site.example/img/i.ico")]
    [InlineData("""<link rel="icon" href="/i.svg"><link rel="apple-touch-icon" href="https://cdn.example/t.png">""", "https://cdn.example/t.png")]
    [InlineData("""<link rel="icon" href="data:image/png;base64,AAAA">""", null)]
    [InlineData("""<link rel="stylesheet" href="/s.css">""", null)]
    public void TheIconAPageNamesIsFound(string html, string? expected)
    {
        Assert.Equal(expected, SiteIcons.DeclaredIcon(html, new Uri("https://site.example/"))?.ToString());
    }

    [Fact]
    public void EachSiteIsKeptOnce()
    {
        Assert.Equal("github.com", SiteIcons.CacheKey(new Uri("https://GitHub.com/pulls")));
        Assert.Equal("localhost_8080", SiteIcons.CacheKey(new Uri("http://localhost:8080/app")));
        Assert.True(SiteIcons.LooksLikeImage(Png));
        Assert.False(SiteIcons.LooksLikeImage("<html>"u8));
    }

    public void Dispose()
    {
        if (Directory.Exists(this.directory))
        {
            Directory.Delete(this.directory, recursive: true);
        }
    }

    private SiteIcons Create() => new(this.converter, new ConfigPaths(this.directory), this.http);

    private static HttpResponseMessage Image(byte[] bytes) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) { Headers = { ContentType = new MediaTypeHeaderValue("image/png") } } };

    private static HttpResponseMessage Page(string html) =>
        new(HttpStatusCode.OK) { Content = new StringContent(html) };
}
