using System.Net;
using System.Text.RegularExpressions;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.Infrastructure.Browser;

/// <summary>
/// Gets a website's tab icon from the site itself - the one open in the tab, so no one else learns of
/// it: first its <c>/favicon.ico</c>, then the icon its home page names. It's turned into a 32×32 PNG
/// with the built-in <c>sips</c> and kept in the icon cache, so a site is asked once; a site with none
/// is noted too, and asked again after a week.
///
/// Kept light: each request gets 2 seconds and at most 100 KB, a home page at most 200 KB, and no
/// cookies are sent. Anything slower or bigger simply has no icon.
/// </summary>
public sealed partial class SiteIcons : ISiteIcons
{
    private const int MostIconBytes = 100 * 1024;
    private const int MostPageBytes = 200 * 1024;
    private const int IconSize = 32;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan AskAgainAfter = TimeSpan.FromDays(7);

    private readonly HttpClient http;
    private readonly IProcessRunner processRunner;
    private readonly string cacheDirectory;

    public SiteIcons(IProcessRunner processRunner, ConfigPaths configPaths, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(configPaths);

        this.processRunner = processRunner;
        this.cacheDirectory = Path.Combine(configPaths.IconCacheDirectory, "sites");
        this.http = new HttpClient(handler ?? new HttpClientHandler { UseCookies = false, MaxAutomaticRedirections = 3 })
        {
            Timeout = RequestTimeout
        };
        this.http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh) ContextSwitcher");
    }

    /// <inheritdoc />
    public async Task<string?> GetIconPathAsync(string pageUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri? page) || (page.Scheme != Uri.UriSchemeHttp && page.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        string key = CacheKey(page);
        string iconPath = Path.Combine(this.cacheDirectory, key + ".png");
        string nonePath = Path.Combine(this.cacheDirectory, key + ".none");
        if (File.Exists(iconPath))
        {
            return iconPath;
        }

        if (File.Exists(nonePath) && DateTime.UtcNow - File.GetLastWriteTimeUtc(nonePath) < AskAgainAfter)
        {
            return null;
        }

        Directory.CreateDirectory(this.cacheDirectory);
        Uri site = new(page.GetLeftPart(UriPartial.Authority) + "/");
        byte[]? icon = await this.GetImageAsync(new Uri(site, "favicon.ico"), cancellationToken).ConfigureAwait(false);
        if (icon is null && await this.FindDeclaredIconAsync(site, cancellationToken).ConfigureAwait(false) is { } declared)
        {
            icon = await this.GetImageAsync(declared, cancellationToken).ConfigureAwait(false);
        }

        if (icon is not null && await this.SaveAsPngAsync(icon, iconPath, cancellationToken).ConfigureAwait(false))
        {
            File.Delete(nonePath);
            return iconPath;
        }

        await File.WriteAllTextAsync(nonePath, string.Empty, cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>The site's host and port, safe as a file name: one icon per site, whichever of its pages is open.</summary>
    public static string CacheKey(Uri page)
    {
        ArgumentNullException.ThrowIfNull(page);
        string host = page.IsDefaultPort ? page.Host : $"{page.Host}_{page.Port}";
        return FileNameUnsafe().Replace(host.ToLowerInvariant(), "_");
    }

    /// <summary>The <c>href</c> of the first icon a page's <c>&lt;link rel="icon"&gt;</c> names, resolved against the site.</summary>
    public static Uri? DeclaredIcon(string html, Uri site)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(site);

        foreach (Match link in LinkTag().Matches(html))
        {
            string tag = link.Value;
            Match rel = Attribute("rel").Match(tag);
            Match href = Attribute("href").Match(tag);
            if (!rel.Success || !href.Success || !rel.Groups["value"].Value.Contains("icon", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = WebUtility.HtmlDecode(href.Groups["value"].Value.Trim());
            if (value.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Uri.TryCreate(site, value, out Uri? resolved) && (resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps))
            {
                return resolved;
            }
        }

        return null;
    }

    /// <summary>Whether the bytes start as a picture <c>sips</c> can read: ICO, PNG, GIF, JPEG or BMP.</summary>
    public static bool LooksLikeImage(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 4 && (
            (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0x01 && bytes[3] == 0x00)
            || (bytes[0] == 0x89 && bytes[1] == (byte)'P' && bytes[2] == (byte)'N' && bytes[3] == (byte)'G')
            || (bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F')
            || (bytes[0] == 0xFF && bytes[1] == 0xD8)
            || (bytes[0] == (byte)'B' && bytes[1] == (byte)'M'));

    private async Task<byte[]?> GetImageAsync(Uri url, CancellationToken cancellationToken)
    {
        byte[]? bytes = await this.GetBytesAsync(url, MostIconBytes, cancellationToken).ConfigureAwait(false);
        return bytes is not null && LooksLikeImage(bytes) ? bytes : null;
    }

    private async Task<Uri?> FindDeclaredIconAsync(Uri site, CancellationToken cancellationToken)
    {
        byte[]? page = await this.GetBytesAsync(site, MostPageBytes, cancellationToken).ConfigureAwait(false);
        return page is null ? null : DeclaredIcon(System.Text.Encoding.UTF8.GetString(page), site);
    }

    /// <summary>The response's body, or <see langword="null"/> for a failure, a timeout, or more than <paramref name="most"/> bytes.</summary>
    private async Task<byte[]?> GetBytesAsync(Uri url, int most, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await this.http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > most)
            {
                return null;
            }

            Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using MemoryStream buffer = new();
                byte[] chunk = new byte[8192];
                int read;
                while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    if (buffer.Length + read > most)
                    {
                        return null;
                    }

                    buffer.Write(chunk, 0, read);
                }

                return buffer.ToArray();
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<bool> SaveAsPngAsync(byte[] icon, string iconPath, CancellationToken cancellationToken)
    {
        string download = iconPath + ".download";
        try
        {
            await File.WriteAllBytesAsync(download, icon, cancellationToken).ConfigureAwait(false);
            ProcessResult result = await this.processRunner
                .RunAsync(new ProcessStartOptions("sips", ["-s", "format", "png", "-Z", IconSize.ToString(System.Globalization.CultureInfo.InvariantCulture), download, "--out", iconPath], RequestTimeout), cancellationToken)
                .ConfigureAwait(false);
            return result.ExitCode == 0 && File.Exists(iconPath);
        }
        finally
        {
            File.Delete(download);
        }
    }

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkTag();

    [GeneratedRegex(@"[^a-z0-9._-]")]
    private static partial Regex FileNameUnsafe();

    private static Regex Attribute(string name) =>
        new($@"\b{name}\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))", RegexOptions.IgnoreCase);
}
