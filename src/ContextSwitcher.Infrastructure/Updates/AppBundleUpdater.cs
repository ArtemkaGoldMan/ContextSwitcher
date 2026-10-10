using System.Diagnostics;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.Infrastructure.Updates;

/// <summary>
/// Installs a newer release over the running app.
///
/// What makes it safe without an Apple Developer ID: every release is signed with the project's own
/// certificate (scripts/create-signing-identity.sh), and a download is installed only if it satisfies
/// the running copy's designated requirement - "this identifier, signed by this certificate". A file
/// swapped on the way, or a release signed by anyone else, is refused. A copy that is not signed that
/// way (built ad-hoc, or run from source) cannot vouch for anything, so it does not update itself.
///
/// A file this process downloads is not quarantined - only browsers and the like mark downloads - so
/// unlike a disk image fetched by hand, the new version opens without the quarantine command.
///
/// The swap happens after this process exits, from a short shell script it starts and leaves running:
/// replacing the bundle under a running .NET app would have it load the rest of its assemblies from
/// the new version. The script is started directly rather than through IProcessRunner because it has
/// to outlive this process, which the runner, killing what it starts when cancelled, is built not to
/// allow.
/// </summary>
public sealed class AppBundleUpdater : IAppUpdater
{
    /// <summary>
    /// Waits for the old copy to quit, swaps the new one in - putting the old one back if that fails -
    /// opens whichever is there, and cleans up. A copy still running after 30 seconds is left alone.
    /// </summary>
    public const string InstallScript = """
        #!/bin/bash
        # Written and started by Context Switcher's updater (AppBundleUpdater.cs).
        pid="$1"; current="$2"; staged="$3"; work="$4"; launcher="$5"
        for _ in $(seq 1 300); do
          kill -0 "$pid" 2>/dev/null || break
          sleep 0.1
        done
        if kill -0 "$pid" 2>/dev/null; then
          exit 1
        fi
        backup="$work/previous.app"
        if mv "$current" "$backup"; then
          if mv "$staged" "$current"; then
            rm -rf "$backup"
          else
            mv "$backup" "$current"
          fi
        fi
        "$launcher" "$current"
        rm -rf "$work"
        """;

    private const string BundleName = "ContextSwitcher.app";

    /// <summary>Well above the ~50 MB a release weighs; a download past this is not ours.</summary>
    private const long MaxArchiveBytes = 400L * 1024 * 1024;

    private static readonly TimeSpan ToolTimeout = TimeSpan.FromMinutes(2);

    private readonly HttpClient http;
    private readonly IProcessRunner processRunner;
    private readonly string? bundlePath;
    private readonly string workRoot;
    private readonly string launcher;

    public AppBundleUpdater(HttpClient http, IProcessRunner processRunner)
        : this(http, processRunner, FindBundle(AppContext.BaseDirectory), Path.GetTempPath(), "/usr/bin/open")
    {
    }

    /// <param name="bundlePath">The running app's bundle, or null when it is not running from one.</param>
    /// <param name="workRoot">Where downloads are unpacked.</param>
    /// <param name="launcher">What opens the app once it is swapped in.</param>
    public AppBundleUpdater(HttpClient http, IProcessRunner processRunner, string? bundlePath, string workRoot, string launcher)
    {
        this.http = http;
        this.processRunner = processRunner;
        this.bundlePath = bundlePath;
        this.workRoot = workRoot;
        this.launcher = launcher;
    }

    /// <summary>
    /// The .app a process runs from, given its base directory (<c>…/X.app/Contents/MacOS/</c>), or
    /// null when it is not in one - <c>dotnet run</c> runs from the build output.
    /// </summary>
    public static string? FindBundle(string baseDirectory)
    {
        DirectoryInfo macOs = new(Path.TrimEndingDirectorySeparator(baseDirectory));
        DirectoryInfo? contents = macOs.Parent;
        DirectoryInfo? bundle = contents?.Parent;

        return macOs.Name == "MacOS" && contents?.Name == "Contents" && bundle is not null
            && bundle.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
            ? bundle.FullName
            : null;
    }

    /// <summary>
    /// The requirement out of <c>codesign -d -r-</c>'s output: what follows "designated =>". An
    /// ad-hoc signature's is implicit and marked with a leading "#".
    /// </summary>
    public static string? ParseDesignatedRequirement(string codesignOutput)
    {
        const string Marker = "designated => ";
        string? line = codesignOutput.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Contains(Marker, StringComparison.Ordinal));
        return line is null ? null : line[(line.IndexOf(Marker, StringComparison.Ordinal) + Marker.Length)..].Trim();
    }

    /// <inheritdoc />
    public async Task<string?> GetInstallBlockerAsync(CancellationToken cancellationToken) =>
        (await this.CheckCanInstallAsync(cancellationToken).ConfigureAwait(false)).Blocker;

    /// <inheritdoc />
    public async Task<StagedUpdate> StageAsync(ReleaseInfo release, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);

        (string? blocker, string? requirement) = await this.CheckCanInstallAsync(cancellationToken).ConfigureAwait(false);
        if (blocker is not null)
        {
            throw new UpdateException(blocker);
        }

        if (release.ArchiveUrl is null)
        {
            throw new UpdateException($"Version {ReleaseVersion.Format(release.Version)} has no {GitHubReleaseSource.ArchiveName} to install from. Download it from its release page instead.");
        }

        string work = Path.Combine(this.workRoot, $"ContextSwitcher-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(work);
        try
        {
            string archive = Path.Combine(work, GitHubReleaseSource.ArchiveName);
            await this.DownloadAsync(release.ArchiveUrl, archive, cancellationToken).ConfigureAwait(false);

            string unpacked = Path.Combine(work, "unpacked");
            ProcessResult unzip = await this.RunAsync("ditto", ["-x", "-k", archive, unpacked], cancellationToken).ConfigureAwait(false);
            string app = Path.Combine(unpacked, BundleName);
            if (unzip.ExitCode != 0 || !Directory.Exists(app))
            {
                throw new UpdateException("The download couldn't be unpacked into the app. Try again, or download it from the release page.");
            }

            Version downloaded = await this.ReadVersionAsync(app, cancellationToken).ConfigureAwait(false);
            if (downloaded != release.Version)
            {
                throw new UpdateException($"The download is version {ReleaseVersion.Format(downloaded)}, not {ReleaseVersion.Format(release.Version)}, so it wasn't installed.");
            }

            ProcessResult verify = await this.RunAsync("codesign", ["--verify", "--deep", "--strict", $"-R={requirement}", app], cancellationToken).ConfigureAwait(false);
            if (verify.ExitCode != 0)
            {
                throw new UpdateException("The download isn't signed with Context Switcher's certificate, so it wasn't installed.");
            }

            return new StagedUpdate(release.Version, app, work);
        }
        catch
        {
            TryDelete(work);
            throw;
        }
    }

    /// <inheritdoc />
    public void InstallOnExit(StagedUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (this.bundlePath is null)
        {
            throw new UpdateException("This copy isn't an installed app, so there is nothing to replace.");
        }

        string script = Path.Combine(update.WorkDirectory, "install.sh");
        File.WriteAllText(script, InstallScript);

        ProcessStartInfo start = new("/bin/bash") { UseShellExecute = false };
        foreach (string argument in new[] { script, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), this.bundlePath, update.AppPath, update.WorkDirectory, this.launcher })
        {
            start.ArgumentList.Add(argument);
        }

        using Process? installer = Process.Start(start);
        if (installer is null)
        {
            throw new UpdateException("The update couldn't be started.");
        }
    }

    private async Task<(string? Blocker, string? Requirement)> CheckCanInstallAsync(CancellationToken cancellationToken)
    {
        if (this.bundlePath is null)
        {
            return ("This copy runs from source rather than as an installed app, so it can't update itself.", null);
        }

        string folder = Path.GetDirectoryName(this.bundlePath)!;
        if (!CanWriteTo(folder))
        {
            return ($"Context Switcher isn't allowed to replace itself in {folder}. Download the new version from its release page.", null);
        }

        ProcessResult display = await this.RunAsync("codesign", ["-d", "-r-", this.bundlePath], cancellationToken).ConfigureAwait(false);
        string? requirement = ParseDesignatedRequirement(display.StandardOutput + "\n" + display.StandardError);
        if (requirement is null || requirement.StartsWith("cdhash", StringComparison.Ordinal))
        {
            return ("This copy isn't signed with the release certificate, so it can't check that an update is genuine. Download the new version from its release page.", null);
        }

        return (null, requirement);
    }

    private async Task DownloadAsync(string url, string destination, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await this.http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new UpdateException($"The download failed ({(int)response.StatusCode}). Try again later.");
            }

            if (response.Content.Headers.ContentLength > MaxArchiveBytes)
            {
                throw new UpdateException("The download is far larger than a release should be, so it wasn't installed.");
            }

            Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                FileStream target = File.Create(destination);
                await using (target.ConfigureAwait(false))
                {
                    byte[] buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        total += read;
                        if (total > MaxArchiveBytes)
                        {
                            throw new UpdateException("The download is far larger than a release should be, so it wasn't installed.");
                        }

                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateException("The download failed. Check the internet connection and try again.", ex);
        }
    }

    private async Task<Version> ReadVersionAsync(string app, CancellationToken cancellationToken)
    {
        ProcessResult result = await this.RunAsync(
            "plutil",
            ["-extract", "CFBundleShortVersionString", "raw", "-o", "-", Path.Combine(app, "Contents", "Info.plist")],
            cancellationToken).ConfigureAwait(false);

        return result.ExitCode == 0 && ReleaseVersion.TryParse(result.StandardOutput, out Version version)
            ? version
            : throw new UpdateException("The download has no version number, so it wasn't installed.");
    }

    private Task<ProcessResult> RunAsync(string tool, IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        this.processRunner.RunAsync(new ProcessStartOptions(tool, arguments, ToolTimeout), cancellationToken);

    /// <summary>Whether a folder accepts new entries, found by making one: permissions alone don't account for ACLs.</summary>
    private static bool CanWriteTo(string folder)
    {
        string probe = Path.Combine(folder, $".ContextSwitcher-update-check-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(probe);
            Directory.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover in the temporary folder; macOS clears those out on its own.
        }
    }
}
