using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.Infrastructure.Updates;

/// <summary>
/// Asks GitHub for the repository's latest release. GitHub's "latest" already leaves out drafts and
/// pre-releases, so a release marked as a pre-release is never offered as an update.
///
/// One unauthenticated request; GitHub allows sixty an hour from an address, and the app makes one a
/// day. Nothing about the Mac or its user is sent beyond what any web request carries.
/// </summary>
public sealed class GitHubReleaseSource : IReleaseSource
{
    /// <summary>The asset the updater installs from: the app, zipped with ditto.</summary>
    public const string ArchiveName = "ContextSwitcher.zip";

    public const string DefaultRepository = "ArtemkaGoldMan/ContextSwitcher";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient http;
    private readonly string repository;
    private readonly string userAgentVersion;

    public GitHubReleaseSource(HttpClient http, string userAgentVersion, string repository = DefaultRepository)
    {
        this.http = http;
        this.userAgentVersion = userAgentVersion;
        this.repository = repository;
    }

    /// <inheritdoc />
    public async Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"https://api.github.com/repos/{this.repository}/releases/latest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ContextSwitcher", this.userAgentVersion));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            using HttpResponseMessage response = await this.http.SendAsync(request, timeout.Token).ConfigureAwait(false);

            // No release at all yet answers 404, which is not an error from the user's point of view.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new UpdateException($"GitHub answered with an error ({(int)response.StatusCode}). Try again later.");
            }

            Stream body = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token).ConfigureAwait(false);
                return Read(document.RootElement);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateException("Couldn't reach GitHub. Check the internet connection.", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpdateException("GitHub didn't answer in time. Try again later.", ex);
        }
        catch (JsonException ex)
        {
            throw new UpdateException("GitHub's answer couldn't be read.", ex);
        }
    }

    private static ReleaseInfo Read(JsonElement release)
    {
        string tag = release.TryGetProperty("tag_name", out JsonElement tagElement) ? tagElement.GetString() ?? string.Empty : string.Empty;
        if (!ReleaseVersion.TryParse(tag, out Version version))
        {
            throw new UpdateException($"The latest release is tagged \"{tag}\", which isn't a version number.");
        }

        string page = release.TryGetProperty("html_url", out JsonElement pageElement) ? pageElement.GetString() ?? string.Empty : string.Empty;

        string? archive = null;
        if (release.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Array)
        {
            archive = assets.EnumerateArray()
                .Where(asset => asset.TryGetProperty("name", out JsonElement name) && name.GetString() == ArchiveName)
                .Select(asset => asset.TryGetProperty("browser_download_url", out JsonElement url) ? url.GetString() : null)
                .FirstOrDefault();
        }

        return new ReleaseInfo(version, page, archive);
    }
}
