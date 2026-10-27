using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Windows.Applications;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Windows.Applications;

/// <summary>The app picker's list, and the icon of each app kept as a PNG so that opening it again is instant.</summary>
public sealed class WindowsInstalledAppsTests : IDisposable
{
    private static readonly byte[] SomePng = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private readonly string root = Path.Combine(Path.GetTempPath(), $"ContextSwitcherTests-{Guid.NewGuid():N}");
    private readonly FakeProcessRunner runner = new();
    private readonly FakeClock clock = new();
    private readonly TestLogger logger = new();
    private readonly FakeAppIcons icons = new();

    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    private static ProcessResult Listing(params (string Name, string Id)[] apps) =>
        new(0, "[" + string.Join(",", apps.Select(app => $$"""{"n":"{{StartMenuRig.Encode(app.Name)}}","i":"{{StartMenuRig.Encode(app.Id)}}"}""")) + "]", string.Empty, TimedOut: false);

    private WindowsInstalledApps Picker(params (string Name, string Id)[] apps)
    {
        this.runner.Enqueue(Listing(apps));
        return new WindowsInstalledApps(
            new StartApps(this.runner, this.clock, this.logger, StartMenuRig.Options),
            this.icons,
            new ConfigPaths(this.root),
            InstalledAppsOptions.Default,
            this.logger);
    }

    [Fact]
    public async Task EachAppComesWithItsIconKeptAsAPngInTheCache()
    {
        this.icons.Pngs["Microsoft.VisualStudioCode"] = SomePng;
        WindowsInstalledApps picker = this.Picker(("Visual Studio Code", "Microsoft.VisualStudioCode"));

        InstalledApp app = Assert.Single(await picker.GetInstalledAppsAsync(CancellationToken.None));

        Assert.Equal("Visual Studio Code", app.Name);
        Assert.Equal(Path.Combine(this.root, "icon-cache", "Visual Studio Code.png"), app.IconPath);
        Assert.Equal(SomePng, await File.ReadAllBytesAsync(app.IconPath!));
        Assert.Equal([("Microsoft.VisualStudioCode", 64)], this.icons.Drawn);
    }

    /// <summary>Drawing is slow and an app's icon rarely changes: a PNG already kept is used as it is.</summary>
    [Fact]
    public async Task AnIconAlreadyKeptIsNotDrawnAgain()
    {
        this.icons.Pngs["a"] = SomePng;
        Directory.CreateDirectory(Path.Combine(this.root, "icon-cache"));
        await File.WriteAllBytesAsync(Path.Combine(this.root, "icon-cache", "Slack.png"), [9, 9, 9]);
        WindowsInstalledApps picker = this.Picker(("Slack", "a"));

        InstalledApp app = Assert.Single(await picker.GetInstalledAppsAsync(CancellationToken.None));

        Assert.Equal([9, 9, 9], await File.ReadAllBytesAsync(app.IconPath!));
        Assert.Empty(this.icons.Drawn);
    }

    [Fact]
    public async Task AnAppWindowsWillNotDrawIsListedWithoutAnIcon()
    {
        this.icons.Pngs["b"] = SomePng;
        WindowsInstalledApps picker = this.Picker(("Slack", "a"), ("Code", "b"));

        IReadOnlyList<InstalledApp> apps = await picker.GetInstalledAppsAsync(CancellationToken.None);

        Assert.Equal(["Code", "Slack"], apps.Select(app => app.Name));
        Assert.NotNull(apps[0].IconPath);
        Assert.Null(apps[1].IconPath);
        Assert.Contains(this.logger.Entries, entry => entry.EventId == "AppIconsNotDrawn" && entry.Message.StartsWith("1 of 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoPartFilesAreLeftBehind()
    {
        this.icons.Pngs["a"] = SomePng;
        WindowsInstalledApps picker = this.Picker(("Slack", "a"));

        await picker.GetInstalledAppsAsync(CancellationToken.None);

        Assert.Equal(["Slack.png"], Directory.GetFiles(Path.Combine(this.root, "icon-cache")).Select(Path.GetFileName));
    }

    /// <summary>A name that is not a file name - "AMD Software: Adrenalin Edition" has a colon - still has an icon.</summary>
    [Fact]
    public async Task AppNamesThatAreNotFileNamesStillGetAnIcon()
    {
        this.icons.Pngs["id"] = SomePng;
        WindowsInstalledApps picker = this.Picker((@"AMD: Software <x>/y?", "id"));

        InstalledApp app = Assert.Single(await picker.GetInstalledAppsAsync(CancellationToken.None));

        Assert.True(File.Exists(app.IconPath));
        Assert.Equal("AMD_ Software _x__y_.png", Path.GetFileName(app.IconPath));
    }

    [Fact]
    public async Task AFolderForIconsThatCannotBeMadeLeavesTheAppsWithoutOne()
    {
        Directory.CreateDirectory(this.root);
        await File.WriteAllTextAsync(Path.Combine(this.root, "icon-cache"), "a file where the folder should be");
        this.icons.Pngs["a"] = SomePng;
        WindowsInstalledApps picker = this.Picker(("Slack", "a"));

        InstalledApp app = Assert.Single(await picker.GetInstalledAppsAsync(CancellationToken.None));

        Assert.Null(app.IconPath);
        Assert.Contains(this.logger.Entries, entry => entry.EventId == "IconCacheNotMade");
        Assert.Empty(this.icons.Drawn);
    }

    /// <summary>The first time takes seconds, so it is done as the app starts; the picker then finds every icon kept.</summary>
    [Fact]
    public async Task WarmingUpDrawsEveryIconSoThePickerFindsThemKept()
    {
        this.icons.Pngs["a"] = SomePng;
        this.icons.Pngs["b"] = SomePng;
        WindowsInstalledApps picker = this.Picker(("Slack", "a"), ("Code", "b"));

        await picker.WarmUpAsync(CancellationToken.None);
        int drawnWhileWarming = this.icons.Drawn.Count;
        IReadOnlyList<InstalledApp> apps = await picker.GetInstalledAppsAsync(CancellationToken.None);

        Assert.Equal(2, drawnWhileWarming);
        Assert.Equal(2, this.icons.Drawn.Count);
        Assert.All(apps, app => Assert.True(File.Exists(app.IconPath)));
        Assert.Single(this.runner.Calls);
    }

    [Fact]
    public async Task NoAppsInTheStartMenuIsNoApps()
    {
        WindowsInstalledApps picker = this.Picker();

        Assert.Empty(await picker.GetInstalledAppsAsync(CancellationToken.None));
        Assert.False(Directory.Exists(Path.Combine(this.root, "icon-cache")));
    }

    [Fact]
    public async Task CancellingStopsTheDrawing()
    {
        this.icons.Pngs["a"] = SomePng;
        WindowsInstalledApps picker = this.Picker(("Slack", "a"));
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => picker.GetInstalledAppsAsync(cancel.Token));
    }

    [Theory]
    [InlineData("Slack", "Slack.png")]
    [InlineData("a/b", "a_b.png")]
    [InlineData("  padded  ", "padded.png")]
    public void ANameBecomesAFileName(string name, string expected)
    {
        Assert.Equal(expected, WindowsInstalledApps.FileNameFor(name));
    }

    [Fact]
    public void ABlankNameHasNoFileName()
    {
        Assert.Throws<ArgumentException>(() => WindowsInstalledApps.FileNameFor(" "));
    }

    private sealed class FakeAppIcons : IAppIcons
    {
        public Dictionary<string, byte[]> Pngs { get; } = [];

        public List<(string AppId, int Size)> Drawn { get; } = [];

        public byte[]? RenderPng(string appId, int size)
        {
            this.Drawn.Add((appId, size));
            return this.Pngs.GetValueOrDefault(appId);
        }
    }
}
