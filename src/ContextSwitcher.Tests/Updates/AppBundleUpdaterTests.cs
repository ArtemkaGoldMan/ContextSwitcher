using System.Diagnostics;
using System.Net;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.Infrastructure.ProcessExecution;
using ContextSwitcher.Infrastructure.Updates;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Updates;

/// <summary>
/// The updater against real bundles, made here from a copy of /usr/bin/true: the real ditto unpacks
/// them and the real plutil reads their version. Only the certificate check is played by a stand-in
/// where it has to pass, since no test machine holds the release certificate.
/// </summary>
public sealed class AppBundleUpdaterTests : IDisposable
{
    private const string Requirement = "identifier \"com.artem.contextswitcher\" and certificate leaf = H\"6763aad3db34f90d5502cc91ce1b62fc1f814fd8\"";

    private static readonly ReleaseInfo Release = new(new Version(0, 2, 0), "https://github.com/x/y/releases/tag/v0.2.0", "https://github.com/x/y/releases/download/v0.2.0/ContextSwitcher.zip");

    private readonly string root = Path.Combine(Path.GetTempPath(), "cs-update-" + Guid.NewGuid().ToString("N"));
    private readonly StubHttpHandler handler = new();

    public AppBundleUpdaterTests() => Directory.CreateDirectory(this.root);

    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    [Theory]
    [InlineData("/Applications/ContextSwitcher.app/Contents/MacOS/", "/Applications/ContextSwitcher.app")]
    [InlineData("/Applications/ContextSwitcher.app/Contents/MacOS", "/Applications/ContextSwitcher.app")]
    [InlineData("/Users/me/Projects/ContextSwitcher/src/ContextSwitcher.App/bin/Debug/net10.0/", null)]
    [InlineData("/Applications/ContextSwitcher.app/Contents/Resources/", null)]
    public void FindsTheBundleItRunsFromIfAny(string baseDirectory, string? expected) =>
        Assert.Equal(expected, AppBundleUpdater.FindBundle(baseDirectory));

    [Theory]
    [InlineData("Executable=/Applications/ContextSwitcher.app/Contents/MacOS/ContextSwitcher\ndesignated => " + Requirement + "\n", Requirement)]
    [InlineData("# designated => cdhash H\"401c03e2040857cfca3df30d5c7e933c8e929414\"", "cdhash H\"401c03e2040857cfca3df30d5c7e933c8e929414\"")]
    [InlineData("ContextSwitcher.app: code object is not signed at all", null)]
    public void ReadsTheDesignatedRequirement(string output, string? expected) =>
        Assert.Equal(expected, AppBundleUpdater.ParseDesignatedRequirement(output));

    [Fact]
    public async Task ACopyRunFromSourceCannotUpdateItself()
    {
        AppBundleUpdater updater = new(new HttpClient(this.handler), new FakeProcessRunner(), bundlePath: null, this.root, "/usr/bin/true");

        string? blocker = await updater.GetInstallBlockerAsync(CancellationToken.None);

        Assert.Contains("runs from source", blocker, StringComparison.Ordinal);
    }

    /// <summary>
    /// An ad-hoc signature vouches only for that one build, so a copy signed that way has nothing to
    /// check a download against - it must not install one. The real codesign reads the signature.
    /// </summary>
    [Fact]
    public async Task AnAdHocSignedCopyCannotUpdateItself()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        string installed = await MakeBundleAsync(Path.Combine(this.root, "Applications"), "0.1.0", signAdHoc: true);
        AppBundleUpdater updater = new(new HttpClient(this.handler), new ProcessRunner(), installed, this.root, "/usr/bin/true");

        string? blocker = await updater.GetInstallBlockerAsync(CancellationToken.None);

        Assert.Contains("isn't signed with the release certificate", blocker, StringComparison.Ordinal);
    }

    /// <summary>
    /// Downloaded, unpacked, its version read and its signature checked against the running copy's
    /// own requirement - the step that keeps out anything not signed with the same certificate.
    /// </summary>
    [Fact]
    public async Task ADownloadIsUnpackedAndCheckedAgainstTheRunningCopysCertificate()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        (AppBundleUpdater updater, CertificateRunner runner) = await this.UpdaterOfferingAsync("0.2.0");

        StagedUpdate staged = await updater.StageAsync(Release, CancellationToken.None);

        Assert.Equal(new Version(0, 2, 0), staged.Version);
        Assert.True(File.Exists(Path.Combine(staged.AppPath, "Contents", "Info.plist")));
        Assert.Contains(runner.Verified, arguments => arguments.Contains($"-R={Requirement}") && arguments[^1] == staged.AppPath);
        Assert.Equal(Release.ArchiveUrl, Assert.Single(this.handler.Requests).RequestUri!.ToString());
    }

    [Fact]
    public async Task ADownloadNotSignedWithTheSameCertificateIsRefusedAndRemoved()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        (AppBundleUpdater updater, CertificateRunner runner) = await this.UpdaterOfferingAsync("0.2.0");
        runner.SignatureMatches = false;

        UpdateException error = await Assert.ThrowsAsync<UpdateException>(() => updater.StageAsync(Release, CancellationToken.None));

        Assert.Contains("isn't signed with Context Switcher's certificate", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetDirectories(this.Work, "ContextSwitcher-update-*"));
    }

    [Fact]
    public async Task ADownloadOfAnotherVersionIsRefused()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        (AppBundleUpdater updater, _) = await this.UpdaterOfferingAsync("0.1.5");

        UpdateException error = await Assert.ThrowsAsync<UpdateException>(() => updater.StageAsync(Release, CancellationToken.None));

        Assert.Contains("is version 0.1.5, not 0.2.0", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetDirectories(this.Work, "ContextSwitcher-update-*"));
    }

    [Fact]
    public async Task AReleaseWithoutTheArchiveIsLeftToTheReleasePage()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        (AppBundleUpdater updater, _) = await this.UpdaterOfferingAsync("0.2.0");

        UpdateException error = await Assert.ThrowsAsync<UpdateException>(() => updater.StageAsync(Release with { ArchiveUrl = null }, CancellationToken.None));

        Assert.Contains("release page", error.Message, StringComparison.Ordinal);
        Assert.Empty(this.handler.Requests);
    }

    /// <summary>
    /// The script that runs after the app quits: the new copy takes the old one's place, gets
    /// opened, and the temporary folder - the old copy with it - is gone.
    /// </summary>
    [Fact]
    public async Task TheInstallScriptSwapsTheNewCopyInAndOpensIt()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        string installed = await MakeBundleAsync(Path.Combine(this.root, "Applications"), "0.1.0", signAdHoc: false);
        string work = Path.Combine(this.root, "work");
        string staged = await MakeBundleAsync(Path.Combine(work, "unpacked"), "0.2.0", signAdHoc: false);
        string opened = Path.Combine(this.root, "opened.txt");
        string launcher = Path.Combine(this.root, "launcher.sh");
        await File.WriteAllTextAsync(launcher, $"#!/bin/bash\necho \"$1\" > '{opened}'\n");
        File.SetUnixFileMode(launcher, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string script = Path.Combine(work, "install.sh");
        await File.WriteAllTextAsync(script, AppBundleUpdater.InstallScript);

        int exitCode = await RunScriptAsync(script, ExitedProcessId(), installed, staged, work, launcher);

        Assert.Equal(0, exitCode);
        Assert.Contains("<string>0.2.0</string>", await File.ReadAllTextAsync(Path.Combine(installed, "Contents", "Info.plist")), StringComparison.Ordinal);
        Assert.Equal(installed, (await File.ReadAllTextAsync(opened)).Trim());
        Assert.False(Directory.Exists(work));
    }

    private string Work => Path.Combine(this.root, "tmp");

    private async Task<(AppBundleUpdater Updater, CertificateRunner Runner)> UpdaterOfferingAsync(string downloadVersion)
    {
        string installed = await MakeBundleAsync(Path.Combine(this.root, "Applications"), "0.1.0", signAdHoc: false);
        string release = await MakeBundleAsync(Path.Combine(this.root, "release"), downloadVersion, signAdHoc: false);
        string archive = Path.Combine(this.root, "ContextSwitcher.zip");
        await Run("ditto", "-c", "-k", "--keepParent", release, archive);
        byte[] bytes = await File.ReadAllBytesAsync(archive);
        this.handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

        Directory.CreateDirectory(this.Work);
        CertificateRunner runner = new();
        return (new AppBundleUpdater(new HttpClient(this.handler), runner, installed, this.Work, "/usr/bin/true"), runner);
    }

    /// <summary>A minimal ContextSwitcher.app of the given version.</summary>
    private static async Task<string> MakeBundleAsync(string parent, string version, bool signAdHoc)
    {
        string app = Path.Combine(parent, "ContextSwitcher.app");
        Directory.CreateDirectory(Path.Combine(app, "Contents", "MacOS"));
        File.Copy("/usr/bin/true", Path.Combine(app, "Contents", "MacOS", "ContextSwitcher"));
        await File.WriteAllTextAsync(Path.Combine(app, "Contents", "Info.plist"), $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>CFBundleIdentifier</key><string>com.artem.contextswitcher</string>
              <key>CFBundleExecutable</key><string>ContextSwitcher</string>
              <key>CFBundlePackageType</key><string>APPL</string>
              <key>CFBundleShortVersionString</key><string>{version}</string>
            </dict>
            </plist>
            """);

        if (signAdHoc)
        {
            await Run("codesign", "--force", "--sign", "-", "--identifier", "com.artem.contextswitcher", app);
        }

        return app;
    }

    private static async Task Run(string tool, params string[] arguments)
    {
        ProcessResult result = await new ProcessRunner().RunAsync(new ProcessStartOptions(tool, arguments, TimeSpan.FromMinutes(1)), CancellationToken.None);
        Assert.True(result.ExitCode == 0, $"{tool} failed: {result.StandardError}");
    }

    private static async Task<int> RunScriptAsync(string script, params string[] arguments)
    {
        ProcessStartInfo start = new("/bin/bash") { UseShellExecute = false };
        start.ArgumentList.Add(script);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        return process.ExitCode;
    }

    /// <summary>The id of a process that has already exited, so the script does not wait.</summary>
    private static string ExitedProcessId()
    {
        using Process done = Process.Start(new ProcessStartInfo("/usr/bin/true") { UseShellExecute = false })!;
        done.WaitForExit();
        return done.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The real tools, except codesign: the installed copy reports the release requirement, and a
    /// download passes or fails the check as the test says.
    /// </summary>
    private sealed class CertificateRunner : IProcessRunner
    {
        private readonly ProcessRunner real = new();

        public bool SignatureMatches { get; set; } = true;

        public List<IReadOnlyList<string>> Verified { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessStartOptions options, CancellationToken cancellationToken)
        {
            if (options.FileName != "codesign")
            {
                return this.real.RunAsync(options, cancellationToken);
            }

            if (options.Arguments[0] == "-d")
            {
                return Task.FromResult(new ProcessResult(0, $"designated => {Requirement}\n", "Executable=…", false));
            }

            this.Verified.Add(options.Arguments);
            return Task.FromResult(this.SignatureMatches
                ? new ProcessResult(0, string.Empty, string.Empty, false)
                : new ProcessResult(3, string.Empty, "test-requirement: code failed to satisfy specified code requirement(s)", false));
        }
    }
}
