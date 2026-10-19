using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.Core.Messages;
using System.Globalization;

namespace ContextSwitcher.Infrastructure.Updates;

/// <summary>
/// Asks GitHub for this platform's newest release: the highest version among the repository's
/// releases that carry this platform's files (<see cref="ReleaseAssets"/>), leaving out drafts and
/// pre-releases. Not GitHub's "latest": once Windows has releases of its own, the latest one may carry
/// none of the Mac's files, and a Mac would be offered an update it can't install.
///
/// One unauthenticated request, for the newest <see cref="PageSize"/> releases; GitHub allows sixty
/// an hour from an address, and the app makes one a day. Nothing about the computer or its user is
/// sent beyond what any web request carries.
/// </summary>
public sealed class GitHubReleaseSource : IReleaseSource
{
    public const string DefaultRepository = "ArtemkaGoldMan/ContextSwitcher";

    /// <summary>How many of the newest releases are looked through: far more than one platform skips in a row.</summary>
    public const int PageSize = 30;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient http;
    private readonly string userAgentVersion;
    private readonly ReleaseAssets assets;
    private readonly string repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubReleaseSource"/> class.
    /// </summary>
    /// <param name="http">Sends the request.</param>
    /// <param name="userAgentVersion">The running version, which the request names itself by.</param>
    /// <param name="assets">The files that make a release this platform's.</param>
    /// <param name="repository">The repository whose releases are asked for, as <c>owner/name</c>.</param>
    public GitHubReleaseSource(HttpClient http, string userAgentVersion, ReleaseAssets assets, string repository = DefaultRepository)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(assets);

        this.http = http;
        this.userAgentVersion = userAgentVersion;
        this.assets = assets;
        this.repository = repository;
    }

    /// <inheritdoc />
    public async Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"https://api.github.com/repos/{this.repository}/releases?per_page={PageSize.ToString(CultureInfo.InvariantCulture)}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ContextSwitcher", this.userAgentVersion));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            using HttpResponseMessage response = await this.http.SendAsync(request, timeout.Token).ConfigureAwait(false);

            // A repository that isn't there answers 404; one with no release yet answers an empty list.
            // Neither is an error from the user's point of view.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new UpdateException(UpdateMessages.GitHubError.With(((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)));
            }

            Stream body = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token).ConfigureAwait(false);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new UpdateException(UpdateMessages.GitHubUnreadable.With());
                }

                return this.Newest(document.RootElement);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateException(UpdateMessages.GitHubUnreachable.With(), ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpdateException(UpdateMessages.GitHubTimedOut.With(), ex);
        }
        catch (JsonException ex)
        {
            throw new UpdateException(UpdateMessages.GitHubUnreadable.With(), ex);
        }
    }

    /// <summary>
    /// The highest version among the published releases with this platform's files, or null when there
    /// is none. Releases without them are passed over whatever their tag says.
    /// </summary>
    private ReleaseInfo? Newest(JsonElement releases)
    {
        ReleaseInfo? newest = null;
        foreach (JsonElement release in releases.EnumerateArray())
        {
            if (IsSet(release, "draft") || IsSet(release, "prerelease")
                || !Assets(release).Any(asset => this.assets.Recognises(asset.Name)))
            {
                continue;
            }

            ReleaseInfo candidate = this.Read(release);
            if (newest is null || candidate.Version > newest.Version)
            {
                newest = candidate;
            }
        }

        return newest;
    }

    private ReleaseInfo Read(JsonElement release)
    {
        string tag = release.TryGetProperty("tag_name", out JsonElement tagElement) ? tagElement.GetString() ?? string.Empty : string.Empty;
        if (!ReleaseVersion.TryParse(tag, out Version version))
        {
            throw new UpdateException(UpdateMessages.TagNotAVersion.With(tag));
        }

        string page = release.TryGetProperty("html_url", out JsonElement pageElement) ? pageElement.GetString() ?? string.Empty : string.Empty;

        string? archive = Assets(release)
            .Where(asset => asset.Name == this.assets.Archive)
            .Select(asset => asset.Url)
            .FirstOrDefault();

        string? description = release.TryGetProperty("body", out JsonElement bodyElement) && bodyElement.ValueKind == JsonValueKind.String
            ? bodyElement.GetString()
            : null;

        return new ReleaseInfo(version, page, archive) { Notes = ReleaseNotes.WhatsNew(description) };
    }

    private static IEnumerable<(string Name, string? Url)> Assets(JsonElement release) =>
        release.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Array
            ? assets.EnumerateArray()
                .Where(asset => asset.TryGetProperty("name", out JsonElement name) && name.ValueKind == JsonValueKind.String)
                .Select(asset => (
                    asset.GetProperty("name").GetString()!,
                    asset.TryGetProperty("browser_download_url", out JsonElement url) ? url.GetString() : null))
            : [];

    private static bool IsSet(JsonElement release, string flag) =>
        release.TryGetProperty(flag, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}
