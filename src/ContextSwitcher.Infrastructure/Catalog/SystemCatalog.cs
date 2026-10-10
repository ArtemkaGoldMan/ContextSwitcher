using System.ComponentModel;
using System.Text.Json;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.AppleScript;

namespace ContextSwitcher.Infrastructure.Catalog;

/// <summary>
/// Reads Shortcuts through the <c>shortcuts</c> CLI, containers through <c>docker</c>, Chromium
/// profiles from the browser's <c>Local State</c> file, and playlists and tabs by scripting the app.
/// </summary>
public sealed class SystemCatalog : ISystemCatalog
{
    /// <summary>
    /// Each of these answers in well under a second when it answers at all. The cap is for the ones
    /// that do not - a Docker CLI waiting on a daemon that is still starting, a browser stuck on an
    /// Automation prompt - so a picker shows its fallback instead of spinning.
    /// </summary>
    private static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Opening Music for its playlists takes a few seconds on a cold start, which is the case
    /// this exists for: the user asked for it.
    /// </summary>
    private static readonly TimeSpan MusicStartTimeout = TimeSpan.FromSeconds(20);

    private static readonly BrowserKind[] TabBrowsers = [BrowserKind.Safari, BrowserKind.Chrome, BrowserKind.Brave];

    private readonly IProcessRunner processRunner;
    private readonly IScriptRunner scriptRunner;
    private readonly string homeDirectory;
    private readonly IReadOnlyList<string> applicationDirectories;

    /// <param name="homeDirectory">Overrides the user's home folder, for tests.</param>
    /// <param name="applicationDirectories">Overrides where browsers are looked for, for tests.</param>
    public SystemCatalog(
        IProcessRunner processRunner,
        IScriptRunner scriptRunner,
        string? homeDirectory = null,
        IReadOnlyList<string>? applicationDirectories = null)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(scriptRunner);

        this.processRunner = processRunner;
        this.scriptRunner = scriptRunner;
        this.homeDirectory = homeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        this.applicationDirectories = applicationDirectories
            ?? ["/Applications", Path.Combine(this.homeDirectory, "Applications")];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetShortcutNamesAsync(CancellationToken cancellationToken)
    {
        ProcessResult? result = await this.TryRunAsync("shortcuts", ["list"], cancellationToken).ConfigureAwait(false);
        return result is { ExitCode: 0 } ? Lines(result.StandardOutput) : [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> GetDockerContainersAsync(CancellationToken cancellationToken)
    {
        ProcessResult? result = await this
            .TryRunAsync("docker", ["ps", "--all", "--format", "{{.Names}}"], cancellationToken)
            .ConfigureAwait(false);

        return result is { ExitCode: 0, TimedOut: false }
            ? Lines(result.StandardOutput).Order(StringComparer.OrdinalIgnoreCase).ToList()
            : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<BrowserProfile> GetBrowserProfiles(BrowserKind browser)
    {
        string? dataDirectory = browser switch
        {
            BrowserKind.Chrome => Path.Combine("Google", "Chrome"),
            BrowserKind.Brave => Path.Combine("BraveSoftware", "Brave-Browser"),
            _ => null
        };

        if (dataDirectory is null)
        {
            return [];
        }

        string localState = Path.Combine(this.homeDirectory, "Library", "Application Support", dataDirectory, "Local State");

        try
        {
            if (!File.Exists(localState))
            {
                return [];
            }

            using FileStream stream = File.OpenRead(localState);
            using JsonDocument document = JsonDocument.Parse(stream);
            return ReadProfiles(document.RootElement);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> GetMusicPlaylistsAsync(bool startMusicIfNeeded, CancellationToken cancellationToken)
    {
        ProcessResult result = await this.scriptRunner
            .RunAsync(
                AppleScriptBuilder.ListMusicPlaylists(startMusicIfNeeded),
                startMusicIfNeeded ? MusicStartTimeout : ListTimeout,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0 || result.StandardOutput.Trim() == AppleScriptBuilder.MusicNotRunningMarker)
        {
            return null;
        }

        return Lines(result.StandardOutput).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OpenTab>> GetOpenTabsAsync(BrowserKind browser, CancellationToken cancellationToken)
    {
        IEnumerable<BrowserKind> browsers = browser == BrowserKind.Default ? TabBrowsers : [browser];

        List<OpenTab> tabs = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (BrowserKind candidate in browsers)
        {
            string appName = AppName(candidate);

            // Scripting an app that is not installed makes AppleScript ask the user to locate it.
            if (!this.IsInstalled(appName))
            {
                continue;
            }

            ProcessResult result = await this.scriptRunner
                .RunAsync(AppleScriptBuilder.ListOpenTabs(appName, candidate == BrowserKind.Safari), ListTimeout, cancellationToken)
                .ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                continue;
            }

            foreach (string line in Lines(result.StandardOutput))
            {
                string[] fields = line.Split(AppleScriptBuilder.OpenTabFieldSeparator, 2);
                string url = fields[0].Trim();

                // Only what a profile can store: settings.json accepts http(s) URLs alone, so a
                // new-tab page or a chrome:// URL would only come back as a validation error.
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                    || !seen.Add(url))
                {
                    continue;
                }

                string title = fields.Length > 1 ? fields[1].Trim() : string.Empty;
                tabs.Add(new OpenTab(title.Length > 0 ? title : uri.Host, url));
            }
        }

        return tabs;
    }

    private static IReadOnlyList<BrowserProfile> ReadProfiles(JsonElement root)
    {
        if (!root.TryGetProperty("profile", out JsonElement profile)
            || !profile.TryGetProperty("info_cache", out JsonElement cache)
            || cache.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        List<BrowserProfile> profiles = [];
        foreach (JsonProperty entry in cache.EnumerateObject())
        {
            string name = entry.Value.ValueKind == JsonValueKind.Object
                && entry.Value.TryGetProperty("name", out JsonElement nameElement)
                && nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;

            profiles.Add(new BrowserProfile(entry.Name, name.Length > 0 ? name : entry.Name));
        }

        // The order the browser's own profile menu uses: Default first, then Profile 1, 2, ... 10
        // by number rather than as text, which would put "Profile 10" before "Profile 2".
        return profiles
            .OrderBy(p => p.Directory == "Default" ? 0 : 1)
            .ThenBy(p => ProfileNumber(p.Directory))
            .ThenBy(p => p.Directory, StringComparer.Ordinal)
            .ToList();
    }

    private static int ProfileNumber(string directory) =>
        int.TryParse(directory.AsSpan(directory.LastIndexOf(' ') + 1), out int number) ? number : int.MaxValue;

    private bool IsInstalled(string appName) =>
        this.applicationDirectories.Any(directory => Directory.Exists(Path.Combine(directory, appName + ".app")));

    /// <summary>
    /// Runs a listing command, or returns <see langword="null"/> when the executable is not there
    /// to run - Docker is optional, and a missing binary must read as "unavailable", not a crash.
    /// </summary>
    private async Task<ProcessResult?> TryRunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            return await this.processRunner
                .RunAsync(new ProcessStartOptions(fileName, arguments, ListTimeout), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static List<string> Lines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string AppName(BrowserKind browser) => browser switch
    {
        BrowserKind.Chrome => "Google Chrome",
        BrowserKind.Brave => "Brave Browser",
        BrowserKind.Safari => "Safari",
        _ => throw new ArgumentOutOfRangeException(nameof(browser), browser, "Default browser has no fixed application name.")
    };
}
