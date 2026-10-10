using System.ComponentModel;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.AppleScript;
using ContextSwitcher.Infrastructure.Catalog;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Catalog;

public sealed class SystemCatalogTests : IDisposable
{
    private readonly string home = Path.Combine(Path.GetTempPath(), "cs-catalog-" + Guid.NewGuid().ToString("N"));
    private readonly string applications;

    public SystemCatalogTests()
    {
        this.applications = Path.Combine(this.home, "Apps");
        Directory.CreateDirectory(this.applications);
    }

    public void Dispose() => Directory.Delete(this.home, recursive: true);

    [Fact]
    public async Task ShortcutsAreReadFromTheShortcutsCli()
    {
        FakeProcessRunner runner = new();
        runner.Enqueue(new ProcessResult(0, "Play/Pause\nContextSwitcher - Focus Work\n\n", string.Empty, false));

        IReadOnlyList<string> names = await this.Catalog(runner).GetShortcutNamesAsync(CancellationToken.None);

        Assert.Equal(["Play/Pause", "ContextSwitcher - Focus Work"], names);
        ProcessStartOptions call = Assert.Single(runner.Calls);
        Assert.Equal("shortcuts", call.FileName);
        Assert.Equal(["list"], call.Arguments);
    }

    [Fact]
    public async Task UnreadableShortcutsAreAnEmptyListRatherThanAnError()
    {
        FakeProcessRunner runner = new() { DefaultResult = new ProcessResult(1, string.Empty, "boom", false) };

        Assert.Empty(await this.Catalog(runner).GetShortcutNamesAsync(CancellationToken.None));
        Assert.Empty(await this.Catalog(new MissingExecutableRunner()).GetShortcutNamesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DockerContainersAreListedByNameRunningOrNot()
    {
        FakeProcessRunner runner = new();
        runner.Enqueue(new ProcessResult(0, "redis\napi-db\nPostgres\n", string.Empty, false));

        IReadOnlyList<string>? containers = await this.Catalog(runner).GetDockerContainersAsync(CancellationToken.None);

        Assert.Equal(["api-db", "Postgres", "redis"], containers);
        Assert.Equal(["ps", "--all", "--format", "{{.Names}}"], Assert.Single(runner.Calls).Arguments);
    }

    /// <summary>
    /// "No containers" and "Docker isn't there" read differently in the picker - one says there is
    /// nothing left to add, the other tells the user to start Docker - so they must not collapse.
    /// </summary>
    [Fact]
    public async Task DockerThatCannotBeReachedIsNullNotEmpty()
    {
        FakeProcessRunner daemonDown = new() { DefaultResult = new ProcessResult(1, string.Empty, "Cannot connect to the Docker daemon", false) };
        FakeProcessRunner hung = new() { DefaultResult = new ProcessResult(-1, string.Empty, string.Empty, true) };
        FakeProcessRunner none = new() { DefaultResult = new ProcessResult(0, string.Empty, string.Empty, false) };

        Assert.Null(await this.Catalog(daemonDown).GetDockerContainersAsync(CancellationToken.None));
        Assert.Null(await this.Catalog(hung).GetDockerContainersAsync(CancellationToken.None));
        Assert.Null(await this.Catalog(new MissingExecutableRunner()).GetDockerContainersAsync(CancellationToken.None));
        Assert.Empty((await this.Catalog(none).GetDockerContainersAsync(CancellationToken.None))!);
    }

    [Fact]
    public void ChromeProfilesAreReadFromLocalStateInTheBrowsersOwnOrder()
    {
        this.WriteLocalState(
            Path.Combine("Google", "Chrome"),
            """
            {
              "profile": {
                "info_cache": {
                  "Profile 10": { "name": "Side project" },
                  "Profile 2": { "name": "Client" },
                  "Default": { "name": "Artem" },
                  "Profile 3": { }
                }
              }
            }
            """);

        IReadOnlyList<BrowserProfile> profiles = this.Catalog().GetBrowserProfiles(BrowserKind.Chrome);

        Assert.Equal(
            [new("Default", "Artem"), new("Profile 2", "Client"), new("Profile 3", "Profile 3"), new("Profile 10", "Side project")],
            profiles);
    }

    [Fact]
    public void BraveReadsItsOwnLocalState()
    {
        this.WriteLocalState(
            Path.Combine("BraveSoftware", "Brave-Browser"),
            """{ "profile": { "info_cache": { "Default": { "name": "Private" } } } }""");

        Assert.Equal([new BrowserProfile("Default", "Private")], this.Catalog().GetBrowserProfiles(BrowserKind.Brave));
        Assert.Empty(this.Catalog().GetBrowserProfiles(BrowserKind.Chrome));
    }

    [Fact]
    public void MissingOrBrokenLocalStateHasNoProfiles()
    {
        Assert.Empty(this.Catalog().GetBrowserProfiles(BrowserKind.Chrome));

        this.WriteLocalState(Path.Combine("Google", "Chrome"), "{ not json");
        Assert.Empty(this.Catalog().GetBrowserProfiles(BrowserKind.Chrome));

        Assert.Empty(this.Catalog().GetBrowserProfiles(BrowserKind.Safari));
        Assert.Empty(this.Catalog().GetBrowserProfiles(BrowserKind.Default));
    }

    [Fact]
    public async Task MusicPlaylistsAreReadOnlyIfMusicIsOpenUnlessAskedToStartIt()
    {
        FakeScriptRunner scripts = new();
        scripts.Enqueue(new ProcessResult(0, AppleScriptBuilder.MusicNotRunningMarker + "\n", string.Empty, false));
        scripts.Enqueue(new ProcessResult(0, "Deep Focus\nRunning\nDeep Focus\n", string.Empty, false));
        SystemCatalog catalog = this.Catalog(scripts: scripts);

        Assert.Null(await catalog.GetMusicPlaylistsAsync(startMusicIfNeeded: false, CancellationToken.None));
        Assert.Equal(["Deep Focus", "Running"], await catalog.GetMusicPlaylistsAsync(startMusicIfNeeded: true, CancellationToken.None));

        Assert.Contains("is not running", scripts.Scripts[0], StringComparison.Ordinal);
        Assert.DoesNotContain("is not running", scripts.Scripts[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task MusicThatRefusesTheScriptIsNull()
    {
        FakeScriptRunner scripts = new() { DefaultResult = new ProcessResult(1, string.Empty, "Not authorized to send Apple events to Music.", false) };

        Assert.Null(await this.Catalog(scripts: scripts).GetMusicPlaylistsAsync(startMusicIfNeeded: true, CancellationToken.None));
    }

    [Fact]
    public async Task OpenTabsKeepOnlyWebPagesAndFallBackToTheHostForATitle()
    {
        this.Install("Google Chrome");
        FakeScriptRunner scripts = new();
        char separator = AppleScriptBuilder.OpenTabFieldSeparator;
        scripts.Enqueue(new ProcessResult(
            0,
            $"https://github.com/pulls{separator}Pull requests\nchrome://newtab/{separator}New Tab\nhttps://example.com/{separator}\nhttps://github.com/pulls{separator}Pull requests\n",
            string.Empty,
            false));

        IReadOnlyList<OpenTab> tabs = await this.Catalog(scripts: scripts).GetOpenTabsAsync(BrowserKind.Chrome, CancellationToken.None);

        Assert.Equal([new OpenTab("Pull requests", "https://github.com/pulls"), new OpenTab("example.com", "https://example.com/")], tabs);
        Assert.Contains("tell application \"Google Chrome\"", Assert.Single(scripts.Scripts), StringComparison.Ordinal);
    }

    /// <summary>
    /// Scripting an app that is not installed makes AppleScript ask the user where it is - a dialog
    /// out of nowhere for opening a picker - so a browser that is not there is never scripted.
    /// </summary>
    [Fact]
    public async Task ABrowserThatIsNotInstalledIsNeverScripted()
    {
        FakeScriptRunner scripts = new();

        Assert.Empty(await this.Catalog(scripts: scripts).GetOpenTabsAsync(BrowserKind.Brave, CancellationToken.None));
        Assert.Empty(scripts.Scripts);
    }

    [Fact]
    public async Task TheDefaultBrowserMeansEveryInstalledOneWithoutRepeats()
    {
        this.Install("Safari");
        this.Install("Google Chrome");
        FakeScriptRunner scripts = new();
        char separator = AppleScriptBuilder.OpenTabFieldSeparator;
        scripts.Enqueue(new ProcessResult(0, $"https://a.example/{separator}A\n", string.Empty, false));
        scripts.Enqueue(new ProcessResult(0, $"https://a.example/{separator}A again\nhttps://b.example/{separator}B\n", string.Empty, false));

        IReadOnlyList<OpenTab> tabs = await this.Catalog(scripts: scripts).GetOpenTabsAsync(BrowserKind.Default, CancellationToken.None);

        Assert.Equal(["https://a.example/", "https://b.example/"], tabs.Select(tab => tab.Url));
        Assert.Equal(2, scripts.Scripts.Count);
        Assert.Contains("name of t", scripts.Scripts[0], StringComparison.Ordinal);
        Assert.Contains("title of t", scripts.Scripts[1], StringComparison.Ordinal);
    }

    private SystemCatalog Catalog(IProcessRunner? runner = null, IScriptRunner? scripts = null) =>
        new(runner ?? new FakeProcessRunner(), scripts ?? new FakeScriptRunner(), this.home, [this.applications]);

    private void Install(string appName) => Directory.CreateDirectory(Path.Combine(this.applications, appName + ".app"));

    private void WriteLocalState(string dataDirectory, string json)
    {
        string directory = Path.Combine(this.home, "Library", "Application Support", dataDirectory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Local State"), json);
    }

    /// <summary>What ProcessRunner does when the executable is not installed: Process.Start throws.</summary>
    private sealed class MissingExecutableRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessStartOptions options, CancellationToken cancellationToken) =>
            throw new Win32Exception(2, "No such file or directory");
    }
}
